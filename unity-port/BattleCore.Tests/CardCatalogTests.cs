using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using BattleCore;

namespace BattleCore.Tests
{
    /// <summary>
    /// The eighty swordsman cards (#188) against card_document/swordsman_cards_v4.md v4.6: the
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

        /// <summary>
        /// §4.1: the attribute patterns of the canon, initial + learned (v4.5: 観察 and 覚悟 moved from Sk
        /// to G+Sk). "M" is the movement effect in v4.4: it is no attribute, so it is read off the face,
        /// not off the declared flags. The canon's table still lists the faces the ten stance cards had
        /// before #257 (A+St 3 / G+St 3 / Sk+St 2 / St+M 2); they are stance only now, so the catalog
        /// declares all fourteen as St and none of those four patterns is left.
        /// </summary>
        [TestCase("A", 15)]
        [TestCase("G", 7)]
        [TestCase("St", 14)]
        [TestCase("Sk", 6)]
        [TestCase("M", 6)]
        [TestCase("A+M", 8)]
        [TestCase("A+Sk", 8)]
        [TestCase("A+G", 5)]
        [TestCase("A+St", 0)]
        [TestCase("G+M", 4)]
        [TestCase("G+Sk", 5)]
        [TestCase("G+St", 0)]
        [TestCase("Sk+M", 2)]
        [TestCase("Sk+St", 0)]
        [TestCase("St+M", 0)]
        public void TheAttributePatterns_MatchTheCanonTally(string pattern, int count)
        {
            var attributes = BattleAttribute.None;
            bool movement = false;
            foreach (string word in pattern.Split('+'))
            {
                if (word == "M") { movement = true; continue; }
                attributes |= word switch
                {
                    "A" => BattleAttribute.Attack,
                    "G" => BattleAttribute.Guard,
                    "St" => BattleAttribute.Stance,
                    "Sk" => BattleAttribute.Skill,
                    _ => throw new ArgumentException(word),
                };
            }
            Assert.That(CardCatalog.All.Count(c => c.Attributes == attributes && AttributeRule.HasMovement(c.Face) == movement), Is.EqualTo(count));
        }

        [Test]
        public void TheCosts_AreTwentyFiveThirtyOneTwentyFour_AndNoCardStartsInColumnFour()
        {
            // §4.4: cost 1 / 2 / 3 = 25 / 31 / 24 (鉄壁の構え went from column 3 to 2 with #257);
            // column 4 is where 集中 and 習熟 lead (K8).
            Assert.Multiple(() =>
            {
                Assert.That(CardCatalog.All.Count(c => c.Cost == 1), Is.EqualTo(25));
                Assert.That(CardCatalog.All.Count(c => c.Cost == 2), Is.EqualTo(31));
                Assert.That(CardCatalog.All.Count(c => c.Cost == 3), Is.EqualTo(24));
                Assert.That(CardCatalog.All.Any(c => c.Column == 4), Is.False);
            });
        }

        /// <summary>§4.1 (v4.5): the cost × attribute table, the whole eighty (the 計 of 初期 / 習得 / 計).</summary>
        [TestCase(BattleAttribute.Attack, 11, 17, 8)]
        [TestCase(BattleAttribute.Guard, 6, 5, 8)]
        [TestCase(BattleAttribute.Skill, 5, 2, 4)]
        [TestCase(BattleAttribute.Stance, 3, 7, 4)]
        public void TheCostsByAttribute_MatchTheCanonTable(BattleAttribute attribute, int one, int two, int three)
        {
            var counted = CardCatalog.All.Where(c => c.Attribute == attribute).ToList();
            Assert.Multiple(() =>
            {
                Assert.That(counted.Count(c => c.Cost == 1), Is.EqualTo(one));
                Assert.That(counted.Count(c => c.Cost == 2), Is.EqualTo(two));
                Assert.That(counted.Count(c => c.Cost == 3), Is.EqualTo(three));
            });
        }

        [Test]
        public void TheCostOneGuardCards_AreTheSixOfV45()
        {
            // §4.1 / §4.4 (v4.5, #352): 観察・後ろ跳び・摺り足・間合い切り and 返しの構え・覚悟.
            Assert.That(CardCatalog.All.Where(c => c.Cost == 1 && c.Attribute == BattleAttribute.Guard).Select(c => c.Id),
                Is.EquivalentTo(new[] { "observe", "back_leap", "slide_step", "break_off", "riposte_stance", "resolve" }));
        }

        [Test]
        public void TheV45Changes_AreInTheCatalog()
        {
            // swordsman_cards_v4 v4.5 (#352): a handful of the changes, read straight off the tables.
            Assert.Multiple(() =>
            {
                Assert.That(CardCatalog.KesaCut.Face.Power, Is.EqualTo(15), "#2: column 2 at 0〜1");
                Assert.That(CardCatalog.BoarRush.Face.Power, Is.EqualTo(11), "#31: two faces at 1〜3");
                Assert.That(CardCatalog.BoarRush.Face.ReachOrDefault, Is.EqualTo(new Reach(1, 3)));
                Assert.That(CardCatalog.ThrowBlade.Face.Power, Is.EqualTo(8), "#9: column 2 at 0〜3");
                Assert.That(CardCatalog.ThrowBlade.Face.ReachOrDefault, Is.EqualTo(new Reach(0, 3)));
                Assert.That(CardCatalog.Thrust.Face.ReachOrDefault, Is.EqualTo(new Reach(0, 2)), "#1: widened, still 23");
                Assert.That(CardCatalog.Thrust.Face.Power, Is.EqualTo(23));
                Assert.That(CardCatalog.GaleThrust.Face.Power, Is.EqualTo(14), "#73: two faces at 2〜3");

                Assert.That(CardCatalog.Observe.Attribute, Is.EqualTo(BattleAttribute.Guard), "#22 is 防御 now");
                Assert.That(CardCatalog.Observe.Face.Guard, Is.EqualTo(3));
                Assert.That(CardCatalog.Observe.Face.Draw, Is.EqualTo(1));
                Assert.That(CardCatalog.BreakOff.Attribute, Is.EqualTo(BattleAttribute.Guard), "#28 is 防御 now");
                Assert.That(CardCatalog.BreakOff.Face.Guard, Is.EqualTo(4));
                Assert.That(CardCatalog.BreakOff.Face.StaminaGain, Is.EqualTo(0), "the Guard replaced スタミナ +1");
                Assert.That(CardCatalog.BreakOff.Trait!.Effect, Is.EqualTo(TraitEffect.StaminaGain), "the trait 締め: スタミナ +1 stays");
                Assert.That(CardCatalog.Resolve.Attribute, Is.EqualTo(BattleAttribute.Guard), "#72 is 防御 now");
                Assert.That(CardCatalog.Resolve.Face.Guard, Is.EqualTo(3));
                Assert.That(CardCatalog.Resolve.Face.StatusList.Single(), Is.EqualTo(new StatusGrant(StatusKind.Empower, 2, OnSelf: true)));

                Assert.That(CardCatalog.IronWall.Cost, Is.EqualTo(2), "#39: column 3 → 2 (#257)");
                Assert.That(CardCatalog.IronWall.Face.Stance, Is.EqualTo(new StanceDef(StanceHook.CostDiscount, Attribute: BattleAttribute.Guard)));
            });
        }

        [Test]
        public void TheV46SideSweep_ReachesTwo_AtTwentyThree()
        {
            // swordsman_cards_v4 v4.6 (#401, followed by #402): #5 横薙ぎ widened from 0〜1 to 0〜2 and
            // dropped from 26 to 23 (column 3 at width 3 is 21, plus 素直 +2), so the stacked desk blow
            // (23 + 9 + 5) × 1.5 rounds to 56, under §13 基準 15's 57. Nothing else on the card moved.
            var card = CardCatalog.SideSweep;
            Assert.Multiple(() =>
            {
                Assert.That(card.Face.Power, Is.EqualTo(23), "#5: 21 at 0〜2 plus 素直 +2");
                Assert.That(card.Face.ReachOrDefault, Is.EqualTo(new Reach(0, 2)));
                Assert.That(card.Cost, Is.EqualTo(3));
                Assert.That(card.Attributes, Is.EqualTo(BattleAttribute.Attack), "a lone attack face");
                Assert.That(card.Trait, Is.Null, "素直");
                Assert.That(card.Targets, Is.EqualTo(TargetKind.One));
            });
        }

        [Test]
        public void TheAttackPowers_AddUpToTheCanonTotal()
        {
            // §4.5: 「攻撃の値の合計は、v4.4 の 351 から v4.5 で 393、v4.6 で 390 になりました」 (初期
            // 204 → 220, 習得 147 → 170), over the 36 attack cards' faces. v4.6 took 3 off #5 横薙ぎ (#401).
            var attacks = CardCatalog.All.Where(c => c.Attribute == BattleAttribute.Attack).ToList();
            Assert.Multiple(() =>
            {
                Assert.That(attacks.Count, Is.EqualTo(36));
                Assert.That(attacks.Sum(c => c.Face.Power), Is.EqualTo(390));
                Assert.That(CardCatalog.All.Take(40).Where(c => c.Attribute == BattleAttribute.Attack).Sum(c => c.Face.Power), Is.EqualTo(220));
                Assert.That(CardCatalog.All.Skip(40).Where(c => c.Attribute == BattleAttribute.Attack).Sum(c => c.Face.Power), Is.EqualTo(170));
            });
        }

        [Test]
        public void EveryAttackFace_ReadsTheBandTable()
        {
            // §1.4 (v4.5): every attack card's power is the band value for its column and reach width —
            // the single row for a lone attack face, the two-face row beside a second face or a
            // movement (§1.1) — plus what the canon adds outside the table: 素直 +2, #79's +2 (#208),
            // and #80 keeping §17.4's 8.
            var off = new List<string>();
            foreach (var card in CardCatalog.All.Where(c => c.Attribute == BattleAttribute.Attack))
            {
                bool single = card.Attributes == BattleAttribute.Attack && !AttributeRule.HasMovement(card.Face);
                int width = Bands.Width(card.Face.ReachOrDefault);
                int band = single ? Bands.Single(width, card.Column) : Bands.Dual(width, card.Column);
                int extra = card.Trait == null || card.Id == "shadow_lunge" ? 2 : 0;
                int expected = card.Id == "last_stand" ? 8 : band + extra;
                if (card.Face.Power != expected) off.Add(card.Id + ": " + card.Face.Power + ", the table says " + expected);
            }
            Assert.That(off, Is.Empty);
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
        [TestCase(TraitCondition.Combo, 8)]
        [TestCase(TraitCondition.Moved, 3)]
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

        /// <summary>
        /// §4.3 (v4.4): the effect tally. #51 brought the traits of 深淵の構え (間合い 0: 追撃), 根縛り
        /// (相手の状態(鈍足): ドロー +1) and 狼の構え (自分の状態(強化): 追撃) to v4.4 (#257).
        /// </summary>
        [TestCase(TraitEffect.StaminaGain, 8)]
        [TestCase(TraitEffect.PowerBonus, 14)]
        [TestCase(TraitEffect.GuardBonus, 15)]
        [TestCase(TraitEffect.Draw, 9)]
        [TestCase(TraitEffect.Status, 6)]
        [TestCase(TraitEffect.CostDown, 3)]
        [TestCase(TraitEffect.NextTurnRecovery, 3)]
        [TestCase(TraitEffect.HeavyBlow, 8)]
        [TestCase(TraitEffect.Convert, 2)]
        [TestCase(TraitEffect.FollowUp, 4)]
        public void TheEffects_MatchTheCanonTally(TraitEffect effect, int count)
        {
            Assert.That(CardCatalog.All.Count(c => c.Trait != null && c.Trait.Effect == effect), Is.EqualTo(count));
        }

        // ---- §1.2 / §4.4: the two rules over the whole table (#51) ----

        /// <summary>
        /// §1.2: 「同じ「条件 + 効果」の組は 3 枚までしか重ねません（条件は括弧の中身と境目の数まで含めて
        /// 同じもの、効果は種類が同じものを数えます）」. A condition is its word with what is in its
        /// brackets — the gap of 間合い, the bar of 温存, the attribute of 連動, the kind of 予兆, the word
        /// of 相手の状態 / 自分の状態; an effect is its kind, whatever its amount or word. 背水の陣's two
        /// traits are two pairs.
        /// </summary>
        private static string PairOf(Trait trait)
        {
            string bracket = trait.Condition switch
            {
                TraitCondition.GapAtMost or TraitCondition.GapAtLeast or TraitCondition.Reserve => trait.Threshold.ToString(),
                TraitCondition.Combo => trait.Attribute.ToString(),
                TraitCondition.OmenIs => trait.Omen?.ToString() ?? "",
                TraitCondition.FoeHas or TraitCondition.SelfHas => trait.Watch?.ToString() ?? "",
                _ => "",
            };
            return trait.Condition + "(" + bracket + ") : " + trait.Effect;
        }

        [Test]
        public void NoConditionAndEffectPair_SitsOnMoreThanThreeCards()
        {
            var pairs = CardCatalog.All.SelectMany(c => c.AllTraits.Select(t => (Pair: PairOf(t), Card: c.Id)))
                .GroupBy(p => p.Pair)
                .ToDictionary(g => g.Key, g => g.Select(p => p.Card).ToList());
            var over = pairs.Where(p => p.Value.Count > Constants.SameTraitMax)
                .Select(p => p.Key + " on " + string.Join(", ", p.Value)).ToList();
            var atTheCap = pairs.Where(p => p.Value.Count == Constants.SameTraitMax).Select(p => p.Key).ToList();

            Assert.Multiple(() =>
            {
                Assert.That(over, Is.Empty, "§1.2: three cards at most share a pair");
                // §4.4 「上限 3。間合い 2 以上 + 威力、予兆(攻撃) + Guard の 2 組が 3 枚」.
                Assert.That(atTheCap, Is.EquivalentTo(new[] { "GapAtLeast(2) : PowerBonus", "OmenIs(Attack) : GuardBonus" }));
                Assert.That(pairs["GapAtLeast(2) : PowerBonus"], Is.EquivalentTo(new[] { "reach_thrust", "boar_rush", "last_stand" }));
                Assert.That(pairs["OmenIs(Attack) : GuardBonus"], Is.EquivalentTo(new[] { "rock_stance", "back_leap", "bulwark" }));
            });
        }

        /// <summary>
        /// §1.2: the effects split in two when the four cells of 間合い are counted — 攻撃 is 威力 +n /
        /// 重撃 / 転換 / 追撃 / a status on the opponent, 防御 is Guard +n / スタミナ +1 / ドロー +1 /
        /// コスト −1 / 次ターン回復 +1 / a status on oneself.
        /// </summary>
        private static bool AttackEffect(Trait trait) => trait.Effect switch
        {
            TraitEffect.PowerBonus or TraitEffect.HeavyBlow or TraitEffect.Convert or TraitEffect.FollowUp => true,
            TraitEffect.Status => trait.Grant != null && !trait.Grant.OnSelf,
            _ => false,
        };

        [TestCase(true, true, new[] { "body_check", "kesa_cut", "fang_rush", "abyss_stance" })]
        [TestCase(true, false, new[] { "last_stand", "reach_thrust", "blood_dance", "boar_rush" })]
        [TestCase(false, true, new[] { "wrist_cut", "shield_bash", "deflect", "low_guard" })]
        [TestCase(false, false, new[] { "dash_in", "throw_blade", "stone_throw", "gale_thrust" })]
        public void TheSixteenGapCards_FillEachCellWithFour_OnColumnsOneTwoTwoThree(bool attack, bool close, string[] cards)
        {
            // §4.4 「攻撃 × 詰めて得 4 / 攻撃 × 離れて得 4 / 防御 × 詰めて得 4 / 防御 × 離れて得 4」, 「各マスの列は
            // 1 / 2 / 2 / 3」 (§1.3 の 2). 詰めて得 is 間合い n 以下, 離れて得 間合い n 以上; 背水の陣 counts by
            // its first trait (K6). The ids are §4.4's table, column 1 first.
            var gapCards = CardCatalog.All.Where(c => c.Trait != null
                && (c.Trait.Condition == TraitCondition.GapAtMost || c.Trait.Condition == TraitCondition.GapAtLeast)).ToList();
            var cell = gapCards.Where(c => AttackEffect(c.Trait!) == attack && (c.Trait!.Condition == TraitCondition.GapAtMost) == close)
                .OrderBy(c => c.Column).ToList();

            Assert.Multiple(() =>
            {
                Assert.That(gapCards, Has.Count.EqualTo(Constants.PositionTraitCards));
                Assert.That(cell.Select(c => c.Id), Is.EquivalentTo(cards));
                Assert.That(cell.Select(c => c.Column), Is.EqualTo(new[] { 1, 2, 2, 3 }));
            });
        }

        [Test]
        public void TheReaches_MatchTheCanonTable()
        {
            // §4.5 (v4.6): 0 → 4, 0〜1 → 18, 1〜2 → 5, 0〜2 → 11, 2〜3 → 1, 1〜3 → 1, 0〜3 → 3, and 37 aim
            // at nobody. The N they reach add up to 100 (36 / 38 / 21 / 5 at N 0 / 1 / 2 / 3): v4.6
            // moved #5 横薙ぎ from 0〜1 to 0〜2 (#401).
            var directed = CardCatalog.All.Where(c => EnemyAi.IsOpponentDirected(c.Attributes, c.Face, c.Targets)).ToList();
            int Count(int min, int max) => directed.Count(c => c.Face.ReachOrDefault.Equals(new Reach(min, max)));
            int At(int gap) => directed.Count(c => c.Face.ReachOrDefault.Contains(gap));
            Assert.Multiple(() =>
            {
                Assert.That(directed, Has.Count.EqualTo(43));
                Assert.That(CardCatalog.All.Count(c => c.Targets == TargetKind.Self), Is.EqualTo(37));
                Assert.That(Count(0, 0), Is.EqualTo(4));
                Assert.That(Count(0, 1), Is.EqualTo(18));
                Assert.That(Count(1, 2), Is.EqualTo(5));
                Assert.That(Count(0, 2), Is.EqualTo(11));
                Assert.That(Count(2, 3), Is.EqualTo(1));
                Assert.That(Count(1, 3), Is.EqualTo(1));
                Assert.That(Count(0, 3), Is.EqualTo(3));
                Assert.That(At(0), Is.EqualTo(36));
                Assert.That(At(1), Is.EqualTo(38));
                Assert.That(At(2), Is.EqualTo(21));
                Assert.That(At(3), Is.EqualTo(5), "§4.5: five reach gap 3");
                Assert.That(directed.Where(c => c.Face.ReachOrDefault.Contains(3)).Select(c => c.Id),
                    Is.EquivalentTo(new[] { "throw_blade", "boar_rush", "stone_throw", "thorn_shot", "gale_thrust" }));
            });
        }

        [Test]
        public void TheStanceCards_AreFourteen_AndEachSetsAStance()
        {
            // §4.1: the four that were stance only from the start and the ten #257 rewrote (A+St 3 /
            // G+St 3 / Sk+St 2 / St+M 2 before it).
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
        public void EveryCard_KeepsTheMultiHitRule()
        {
            // §2.4 hits (#253): no card whose attack face strikes twice or more gives the opponent a status.
            Assert.That(CardCatalog.All.Where(c => !Cards.MultiHitCarriesNoFoeStatus(c)).Select(c => c.Id), Is.Empty);
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

        /// <summary>A legal deck (§8): at most three of a kind (no cap on stance cards since v4.4), drawn from the eighty with the given RNG.</summary>
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

    /// <summary>
    /// swordsman_cards_v4 §1.4 (v4.5, #352): the band table of attack power by reach width and
    /// column, written out from the canon. The core keeps no copy: the catalog's numbers are the
    /// canon's, and these tests read them back against it.
    /// </summary>
    internal static class Bands
    {
        // Rows are widths 1 to 5, entries columns 1 to 4.
        private static readonly int[][] SingleRows =
        {
            new[] { 8, 17, 27, 36 },
            new[] { 7, 15, 24, 33 },
            new[] { 6, 13, 21, 30 },
            new[] { 5, 10, 17, 26 },
            new[] { 4, 8, 13, 22 },
        };

        private static readonly int[][] DualRows =
        {
            new[] { 5, 11, 18, 24 },
            new[] { 5, 10, 16, 22 },
            new[] { 4, 8, 14, 20 },
            new[] { 3, 7, 11, 17 },
            new[] { 2, 5, 8, 14 },
        };

        /// <summary>A lone attack face (単独の攻撃面).</summary>
        public static int Single(int width, int column) => SingleRows[width - 1][column - 1];

        /// <summary>The attack face of a card with two faces (面を 2 つ持つ札の攻撃面).</summary>
        public static int Dual(int width, int column) => DualRows[width - 1][column - 1];

        /// <summary>§1.4: the number of gaps a reach covers, one more when it reaches 3.</summary>
        public static int Width(Reach reach) => reach.Max - reach.Min + 1 + (reach.Contains(3) ? 1 : 0);
    }
}
