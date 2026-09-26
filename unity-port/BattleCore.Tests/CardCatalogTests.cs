using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using BattleCore;

namespace BattleCore.Tests
{
    /// <summary>
    /// The eighty swordsman cards (#188) against card_document/swordsman_cards_v4.md v4.3: the
    /// catalog holds every one of them, its tallies match the canon's §4 tables, and every card can
    /// be played on some board without the loop throwing.
    /// </summary>
    public class CardCatalogTests
    {
        private static readonly IRng NoShuffle = new FixedRng(0.9999999);

        // ---- The catalog ----

        [Test]
        public void TheCatalog_HoldsEightyKinds_WithUniqueIds()
        {
            Assert.Multiple(() =>
            {
                Assert.That(CardCatalog.All.Count, Is.EqualTo(Constants.OwnedKindsMax));
                Assert.That(CardCatalog.All.Select(c => c.Id).Distinct().Count(), Is.EqualTo(80));
                Assert.That(CardCatalog.All.All(c => c != null), Is.True, "a field read before it was set");
                foreach (var card in CardCatalog.All) Assert.That(CardCatalog.ById(card.Id), Is.SameAs(card), card.Id);
            });
        }

        [Test]
        public void TheCanonOrder_StartsWithThrust_AndEndsWithLastStand()
        {
            Assert.Multiple(() =>
            {
                Assert.That(CardCatalog.All[0].Id, Is.EqualTo("thrust"));
                Assert.That(CardCatalog.All[39].Id, Is.EqualTo("twist_away"), "#40, the last of the initial forty");
                Assert.That(CardCatalog.All[40].Id, Is.EqualTo("miasma_blade"), "#41, the first learned card");
                Assert.That(CardCatalog.All[79].Id, Is.EqualTo("last_stand"));
            });
        }

        /// <summary>§4.1: the fifteen attribute patterns, initial + learned.</summary>
        [TestCase("A", 15)]
        [TestCase("G", 7)]
        [TestCase("St", 4)]
        [TestCase("Sk", 8)]
        [TestCase("M", 6)]
        [TestCase("A+M", 8)]
        [TestCase("A+Sk", 8)]
        [TestCase("A+G", 5)]
        [TestCase("A+St", 3)]
        [TestCase("G+M", 4)]
        [TestCase("G+Sk", 3)]
        [TestCase("G+St", 3)]
        [TestCase("Sk+M", 2)]
        [TestCase("Sk+St", 2)]
        [TestCase("St+M", 2)]
        public void TheAttributePatterns_MatchTheCanonTally(string pattern, int count)
        {
            var attributes = BattleAttribute.None;
            foreach (string word in pattern.Split('+'))
            {
                attributes |= word switch
                {
                    "A" => BattleAttribute.Attack,
                    "G" => BattleAttribute.Guard,
                    "St" => BattleAttribute.Stance,
                    "Sk" => BattleAttribute.Skill,
                    "M" => BattleAttribute.Move,
                    _ => throw new ArgumentException(word),
                };
            }
            Assert.That(CardCatalog.All.Count(c => c.Attributes == attributes), Is.EqualTo(count));
        }

        [Test]
        public void TheCosts_AreTwentyFiveThirtyTwentyFive_AndNoCardStartsInColumnFour()
        {
            // §4.4: cost 1 / 2 / 3 = 25 / 30 / 25; column 4 is where 集中 and 習熟 lead (K8).
            Assert.Multiple(() =>
            {
                Assert.That(CardCatalog.All.Count(c => c.Cost == 1), Is.EqualTo(25));
                Assert.That(CardCatalog.All.Count(c => c.Cost == 2), Is.EqualTo(30));
                Assert.That(CardCatalog.All.Count(c => c.Cost == 3), Is.EqualTo(25));
                Assert.That(CardCatalog.All.Any(c => c.Column == 4), Is.False);
            });
        }

        [Test]
        public void TheTraits_MatchTheCanonTally()
        {
            // §4.2 / §4.4: 72 cards carry a trait, the eight plain ones (素直) none, and 背水の陣 is
            // the one card with a second.
            Assert.Multiple(() =>
            {
                Assert.That(CardCatalog.All.Count(c => c.Trait != null), Is.EqualTo(72));
                Assert.That(CardCatalog.All.Count(c => c.ExtraTrait != null), Is.EqualTo(1));
                Assert.That(CardCatalog.LastStand.ExtraTrait!.Condition, Is.EqualTo(TraitCondition.Desperate));
                Assert.That(CardCatalog.All.Where(c => c.Trait != null).Any(c => c.Trait!.Condition == TraitCondition.Unguarded),
                    Is.False, "無防備 is the enemy-side word (§17.6 F1)");
            });
        }

        /// <summary>§4.2: the condition tally (the #80 second trait is not counted).</summary>
        [TestCase(TraitCondition.Combo, 11)]
        [TestCase(TraitCondition.OmenIs, 10)]
        [TestCase(TraitCondition.Reserve, 2)]
        [TestCase(TraitCondition.Desperate, 1)]
        [TestCase(TraitCondition.FirstPlay, 4)]
        [TestCase(TraitCondition.Finisher, 3)]
        [TestCase(TraitCondition.Broken, 4)]
        [TestCase(TraitCondition.Chain, 4)]
        [TestCase(TraitCondition.Thin, 2)]
        [TestCase(TraitCondition.FoeHas, 9)]
        [TestCase(TraitCondition.SelfHas, 6)]
        public void TheConditions_MatchTheCanonTally(TraitCondition condition, int count)
        {
            Assert.That(CardCatalog.All.Count(c => c.Trait != null && c.Trait.Condition == condition), Is.EqualTo(count));
        }

        [Test]
        public void TheGapConditions_AreSixteen()
        {
            // §4.2 / §4.4: 間合い counts 16 (#80 among them).
            Assert.That(CardCatalog.All.Count(c => c.Trait != null
                && (c.Trait.Condition == TraitCondition.GapAtMost || c.Trait.Condition == TraitCondition.GapAtLeast)),
                Is.EqualTo(Constants.PositionTraitCards));
        }

        /// <summary>§4.3: the effect tally.</summary>
        [TestCase(TraitEffect.StaminaGain, 8)]
        [TestCase(TraitEffect.PowerBonus, 15)]
        [TestCase(TraitEffect.GuardBonus, 15)]
        [TestCase(TraitEffect.Draw, 8)]
        [TestCase(TraitEffect.Status, 7)]
        [TestCase(TraitEffect.CostDown, 3)]
        [TestCase(TraitEffect.NextTurnRecovery, 3)]
        [TestCase(TraitEffect.HeavyBlow, 9)]
        [TestCase(TraitEffect.Convert, 2)]
        [TestCase(TraitEffect.FollowUp, 2)]
        public void TheEffects_MatchTheCanonTally(TraitEffect effect, int count)
        {
            Assert.That(CardCatalog.All.Count(c => c.Trait != null && c.Trait.Effect == effect), Is.EqualTo(count));
        }

        [Test]
        public void TheReaches_MatchTheCanonTable()
        {
            // §4.5: 0 → 4, 0〜1 → 24, 0〜2 → 8, 1〜2 → 7, 1〜3 → 3, 2〜3 → 1, and 33 aim at nobody.
            var directed = CardCatalog.All.Where(c => EnemyAi.IsOpponentDirected(c.Attributes, c.Face, c.Targets)).ToList();
            int Count(int min, int max) => directed.Count(c => c.Face.ReachOrDefault.Equals(new Reach(min, max)));
            Assert.Multiple(() =>
            {
                Assert.That(directed, Has.Count.EqualTo(47));
                Assert.That(CardCatalog.All.Count(c => c.Targets == TargetKind.Self), Is.EqualTo(33));
                Assert.That(Count(0, 0), Is.EqualTo(4));
                Assert.That(Count(0, 1), Is.EqualTo(24));
                Assert.That(Count(0, 2), Is.EqualTo(8));
                Assert.That(Count(1, 2), Is.EqualTo(7));
                Assert.That(Count(1, 3), Is.EqualTo(3));
                Assert.That(Count(2, 3), Is.EqualTo(1));
                Assert.That(directed.Count(c => c.Face.ReachOrDefault.Contains(3)), Is.EqualTo(4), "§4.5: four reach gap 3");
            });
        }

        [Test]
        public void TheStanceCards_AreFourteen_AndEachSetsAStance()
        {
            // §4.1: St 4 + A+St 3 + G+St 3 + Sk+St 2 + St+M 2.
            var stances = CardCatalog.All.Where(c => c.Attributes.HasFlag(BattleAttribute.Stance)).ToList();
            Assert.Multiple(() =>
            {
                Assert.That(stances, Has.Count.EqualTo(14));
                foreach (var card in stances) Assert.That(card.Face.Stance, Is.Not.Null, card.Id);
                Assert.That(CardCatalog.All.Where(c => !c.Attributes.HasFlag(BattleAttribute.Stance)).All(c => c.Face.Stance == null), Is.True);
            });
        }

        [Test]
        public void TheFaces_KeepToTheCellLimits_AndTheStatusWordsPointTheCanonWay()
        {
            Assert.Multiple(() =>
            {
                foreach (var card in CardCatalog.All)
                {
                    Assert.That(Math.Abs(card.Face.Move), Is.LessThanOrEqualTo(Constants.MoveStepMax), card.Id);
                    Assert.That(Math.Abs(card.Face.Push), Is.LessThanOrEqualTo(Constants.MoveStepMax), card.Id);
                    foreach (var grant in card.Face.StatusList)
                    {
                        // §5 向き: 強化 / 集中 / 見切り / 再生 are the holder's own, the rest are put on the opponent.
                        Assert.That(grant.OnSelf, Is.EqualTo(Statuses.IsOwn(grant.Kind)), card.Id + " " + grant.Kind);
                        Assert.That(grant.Stacks, Is.InRange(1, 4), card.Id);
                    }
                }
                // §1.1: the two cards that move the opponent.
                Assert.That(CardCatalog.All.Where(c => c.Face.Push != 0).Select(c => c.Id), Is.EquivalentTo(new[] { "haul_step", "shield_push" }));
            });
        }

        [Test]
        public void ThePrototypeDeck_StillNamesTheTenSliceCards()
        {
            Assert.That(PrototypeDeck.Kinds.Select(c => c.Id), Is.EqualTo(new[]
            {
                "thrust", "kesa_cut", "reach_thrust", "brace", "feint",
                "boar_rush", "body_check", "step_in_guard", "step_out_guard", "shield_bash",
            }));
        }

        // ---- Every card, played ----

        /// <summary>
        /// §2 / §3: each of the eighty, dealt first into a hand of fillers at every opening gap 0〜3,
        /// is played wherever the core lets it, and the battle then runs three more turns. Nothing
        /// may throw, and every card has a gap it can be played from.
        /// </summary>
        [Test]
        public void EveryCard_CanBePlayedOnSomeBoard_AndNothingThrows()
        {
            var neverPlayed = new List<string>();
            foreach (var card in CardCatalog.All)
            {
                bool played = false;
                foreach (int gap in new[] { 0, 1, 2, 3 })
                {
                    var deck = new List<CardInstance> { new CardInstance(card.Id + "-0", card) };
                    for (int i = 0; i < 19; i++) deck.Add(new CardInstance("filler-" + i, CardCatalog.Brace));
                    var setup = new BattleSetup(Enemies.PolearmWarped, deck, BattleSetup.SliceFieldCells, StartGap: gap);
                    var state = TurnLoop.Start(setup, NoShuffle).State;
                    state = TurnLoop.BeginPlayerTurn(state, NoShuffle).State;
                    if (TurnLoop.CanPlay(state, card.Id + "-0") != PlayRefusal.None) continue;

                    played = true;
                    string where = card.Id + " at gap " + gap;
                    Assert.DoesNotThrow(() =>
                    {
                        var after = TurnLoop.PlayCard(state, card.Id + "-0", NoShuffle).State;
                        for (int turn = 0; turn < 3 && after.Result == GameResult.Ongoing; turn++)
                        {
                            after = TurnLoop.EndTurn(after, NoShuffle).State;
                            if (after.Result != GameResult.Ongoing) break;
                            after = TurnLoop.BeginPlayerTurn(after, NoShuffle).State;
                        }
                    }, where);
                }
                if (!played) neverPlayed.Add(card.Id);
            }
            Assert.That(neverPlayed, Is.Empty, "cards no opening gap lets the player release");
        }

        /// <summary>
        /// The whole catalog in play: random legal decks from the eighty fight the polearm to the end
        /// with the leftmost payable card each time (the auto-player the screen's unattended run
        /// uses). The full sweep over every enemy and many seeds is #192's; this holds the line that
        /// the eighty alone never break the loop.
        /// </summary>
        [Test]
        public void RandomDecksOfTheEighty_FightThePolearmToTheEnd()
        {
            for (int seed = 1; seed <= 30; seed++)
            {
                var rng = new SeededRng(seed);
                var deck = RandomDeck(rng, 20 + (seed % 21));
                Assert.That(Cards.Validate(deck).Ok, Is.True);

                var state = TurnLoop.Start(new BattleSetup(Enemies.PolearmWarped, deck, BattleSetup.SliceFieldCells), rng).State;
                int turns = 0;
                Assert.DoesNotThrow(() =>
                {
                    while (state.Result == GameResult.Ongoing && turns < 60)
                    {
                        state = TurnLoop.BeginPlayerTurn(state, rng).State;
                        turns++;
                        while (state.Result == GameResult.Ongoing)
                        {
                            var next = state.Hand.FirstOrDefault(c => TurnLoop.CanPlay(state, c.InstanceId) == PlayRefusal.None);
                            if (next == null) break;
                            state = TurnLoop.PlayCard(state, next.InstanceId, rng).State;
                        }
                        if (state.Result == GameResult.Ongoing) state = TurnLoop.EndTurn(state, rng).State;
                    }
                }, "seed " + seed);
                Assert.That(state.Result, Is.Not.EqualTo(GameResult.Ongoing), "seed " + seed + " did not end in 60 turns");

                // The piles plus the exile pile always hold the whole deck (§4, §8).
                int cards = state.Hand.Count + state.DrawPile.Count + state.DiscardPile.Count + state.Exiled.Count;
                Assert.That(cards, Is.EqualTo(deck.Count), "seed " + seed);
            }
        }

        /// <summary>A legal deck (§8): at most three of a kind, drawn from the eighty with the given RNG.</summary>
        private static List<CardInstance> RandomDeck(IRng rng, int size)
        {
            var counts = new Dictionary<string, int>();
            var deck = new List<CardInstance>();
            while (deck.Count < size)
            {
                var def = CardCatalog.All[(int)(rng.NextDouble() * CardCatalog.All.Count) % CardCatalog.All.Count];
                counts.TryGetValue(def.Id, out int held);
                if (held >= Constants.CopiesMax) continue;
                counts[def.Id] = held + 1;
                deck.Add(new CardInstance(def.Id + "-" + held, def));
            }
            return deck;
        }
    }
}
