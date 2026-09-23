using System.Linq;
using NUnit.Framework;
using BattleCore;

namespace BattleCore.Tests
{
    /// <summary>
    /// The ten prototype cards (#71) against card_document/swordsman_cards_v4.md: every number is
    /// the canon's, the deck passes §8, and the selection covers what the slice needs to show.
    /// </summary>
    public class PrototypeDeckTests
    {
        // ---- The canon rows ----

        //        id                name            column power guard
        [TestCase("thrust",         "突き",         3,     23,   0)]
        [TestCase("kesa_cut",       "袈裟斬り",     2,     13,   0)]
        [TestCase("reach_thrust",   "伸び突き",     2,     13,   0)]
        [TestCase("body_check",     "体当たり",     1,     4,    0)]
        [TestCase("brace",          "呼吸を整える", 2,     0,    9)]
        [TestCase("shield_bash",    "盾打ち",       2,     8,    6)]
        [TestCase("feint",          "牽制",         2,     8,    0)]
        [TestCase("boar_rush",      "猪突猛進",     3,     14,   0)]
        [TestCase("step_in_guard",  "足捌き・前",   3,     0,    12)]
        [TestCase("step_out_guard", "足捌き・後",   3,     0,    12)]
        public void Numbers_MatchTheCanon(string id, string name, int column, int power, int guard)
        {
            var card = CardCatalog.ById(id);
            Assert.Multiple(() =>
            {
                Assert.That(card.Name, Is.EqualTo(name));
                Assert.That(card.Column, Is.EqualTo(column));
                Assert.That(card.Cost, Is.EqualTo(column));
                Assert.That(card.Face.Power, Is.EqualTo(power));
                Assert.That(card.Face.Guard, Is.EqualTo(guard));
            });
        }

        [Test]
        public void Attributes_MatchTheCanon()
        {
            const BattleAttribute A = BattleAttribute.Attack;
            const BattleAttribute G = BattleAttribute.Guard;
            const BattleAttribute M = BattleAttribute.Move;
            const BattleAttribute Sk = BattleAttribute.Skill;

            Assert.Multiple(() =>
            {
                Assert.That(CardCatalog.Thrust.Attributes, Is.EqualTo(A));
                Assert.That(CardCatalog.KesaCut.Attributes, Is.EqualTo(A));
                Assert.That(CardCatalog.ReachThrust.Attributes, Is.EqualTo(A));
                Assert.That(CardCatalog.BodyCheck.Attributes, Is.EqualTo(A | Sk));
                Assert.That(CardCatalog.Brace.Attributes, Is.EqualTo(G));
                Assert.That(CardCatalog.ShieldBash.Attributes, Is.EqualTo(A | G));
                Assert.That(CardCatalog.Feint.Attributes, Is.EqualTo(A | M));
                Assert.That(CardCatalog.BoarRush.Attributes, Is.EqualTo(A | M));
                Assert.That(CardCatalog.StepInGuard.Attributes, Is.EqualTo(G | M));
                Assert.That(CardCatalog.StepOutGuard.Attributes, Is.EqualTo(G | M));
            });
        }

        [Test]
        public void Traits_MatchTheCanon()
        {
            Assert.Multiple(() =>
            {
                Assert.That(CardCatalog.Thrust.Trait, Is.Null, "素直");
                Assert.That(CardCatalog.StepInGuard.Trait, Is.Null, "素直");
                Assert.That(CardCatalog.StepOutGuard.Trait, Is.Null, "素直");

                // v4.3 provisional mapping (#162): 近間 → 間合い 0 以下, 遠間 → 間合い 1 以上 (2 for 猪突猛進).
                Assert.That(CardCatalog.KesaCut.Trait, Is.EqualTo(
                    new Trait(TraitCondition.GapAtMost, TraitEffect.PowerBonus, 5, Threshold: 0)));
                Assert.That(CardCatalog.ReachThrust.Trait, Is.EqualTo(
                    new Trait(TraitCondition.GapAtLeast, TraitEffect.PowerBonus, 5, Threshold: 1)));
                Assert.That(CardCatalog.BoarRush.Trait, Is.EqualTo(
                    new Trait(TraitCondition.GapAtLeast, TraitEffect.PowerBonus, 6, Threshold: 2)));
                Assert.That(CardCatalog.ShieldBash.Trait, Is.EqualTo(
                    new Trait(TraitCondition.GapAtMost, TraitEffect.GuardBonus, 3, Threshold: 0)));
                Assert.That(CardCatalog.BodyCheck.Trait, Is.EqualTo(
                    new Trait(TraitCondition.GapAtMost, TraitEffect.HeavyBlow, 0, Threshold: 0)));
                Assert.That(CardCatalog.Brace.Trait, Is.EqualTo(
                    new Trait(TraitCondition.Reserve, TraitEffect.NextTurnRecovery, 1, Threshold: 6)));
                Assert.That(CardCatalog.Feint.Trait, Is.EqualTo(
                    new Trait(TraitCondition.Reserve, TraitEffect.GuardBonus, 3, Threshold: 4)));
            });
        }

        [Test]
        public void BodyCheck_AppliesTwoStacksOfSlow()
        {
            Assert.That(CardCatalog.BodyCheck.Face.Status, Is.EqualTo(StatusKind.Slow));
            Assert.That(CardCatalog.BodyCheck.Face.StatusStacks, Is.EqualTo(2));
        }

        [Test]
        public void MoveFaces_PointWhereTheCanonSays()
        {
            // 遠間へ → 後ろへ 1, 近間へ → 前へ 1; the boar rush closes two (§21.4's provisional value).
            Assert.Multiple(() =>
            {
                Assert.That(CardCatalog.Feint.Face.Move, Is.EqualTo(-1));
                Assert.That(CardCatalog.StepOutGuard.Face.Move, Is.EqualTo(-1));
                Assert.That(CardCatalog.BoarRush.Face.Move, Is.EqualTo(2));
                Assert.That(CardCatalog.StepInGuard.Face.Move, Is.EqualTo(1));
            });
        }

        [Test]
        public void Reaches_AreTheProvisionalOnes_AndNoneExceedsTheCommonBand()
        {
            // §7.2: the common N is 0〜3; §2.4: the default reach is 0〜1.
            Assert.Multiple(() =>
            {
                Assert.That(CardCatalog.Thrust.Face.ReachOrDefault, Is.EqualTo(Reach.Default));
                Assert.That(CardCatalog.KesaCut.Face.ReachOrDefault, Is.EqualTo(Reach.Default));
                Assert.That(CardCatalog.Feint.Face.ReachOrDefault, Is.EqualTo(Reach.Default));
                Assert.That(CardCatalog.ReachThrust.Face.ReachOrDefault, Is.EqualTo(new Reach(1, 2)));
                Assert.That(CardCatalog.BoarRush.Face.ReachOrDefault, Is.EqualTo(new Reach(1, 2)));
                Assert.That(CardCatalog.BodyCheck.Face.ReachOrDefault, Is.EqualTo(Reach.Only(0)));
                Assert.That(CardCatalog.ShieldBash.Face.ReachOrDefault, Is.EqualTo(Reach.Only(0)));
                foreach (var card in CardCatalog.All) Assert.That(card.Face.ReachOrDefault.Max, Is.LessThanOrEqualTo(3), card.Id);
            });
        }

        [Test]
        public void TheNumbers_SitOnTheColumnRuler()
        {
            // §3.1: single-attribute faces read the ruler directly; 素直 adds 2; two-attribute faces
            // read the dual ruler. A card that drifts off the ruler is a typo.
            Assert.Multiple(() =>
            {
                Assert.That(CardCatalog.Thrust.Face.Power, Is.EqualTo(Columns.Value(Columns.SingleAttackPower, 3) + 2));
                Assert.That(CardCatalog.KesaCut.Face.Power, Is.EqualTo(Columns.Value(Columns.SingleAttackPower, 2)));
                Assert.That(CardCatalog.Brace.Face.Guard, Is.EqualTo(Columns.Value(Columns.SingleGuard, 2)));
                Assert.That(CardCatalog.BodyCheck.Face.Power, Is.EqualTo(Columns.Value(Columns.DualAttackPower, 1)));
                Assert.That(CardCatalog.Feint.Face.Power, Is.EqualTo(Columns.Value(Columns.DualAttackPower, 2)));
                Assert.That(CardCatalog.BoarRush.Face.Power, Is.EqualTo(Columns.Value(Columns.DualAttackPower, 3)));
                Assert.That(CardCatalog.ShieldBash.Face.Guard, Is.EqualTo(Columns.Value(Columns.DualGuard, 2)));
                Assert.That(CardCatalog.StepInGuard.Face.Guard, Is.EqualTo(Columns.Value(Columns.DualGuard, 3) + 2));
            });
        }

        // ---- What the selection has to cover (#71) ----

        [Test]
        public void ThereAreTenKinds_WithUniqueIds()
        {
            Assert.That(CardCatalog.All, Has.Count.EqualTo(10));
            Assert.That(CardCatalog.All.Select(c => c.Id).Distinct().Count(), Is.EqualTo(10));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void EveryCostHasAnAttack(int column)
        {
            Assert.That(
                CardCatalog.All.Any(c => c.Column == column && c.Attributes.HasFlag(BattleAttribute.Attack)),
                Is.True);
        }

        [Test]
        public void TheSelection_CoversGuard_BothDirections_AndSlow()
        {
            var all = CardCatalog.All;
            Assert.Multiple(() =>
            {
                Assert.That(all.Count(c => c.Face.Guard > 0), Is.GreaterThanOrEqualTo(2));
                Assert.That(all.Count(c => c.Face.Move > 0), Is.GreaterThanOrEqualTo(1));
                Assert.That(all.Count(c => c.Face.Move < 0), Is.GreaterThanOrEqualTo(1));
                Assert.That(all.Count(c => c.Face.Status == StatusKind.Slow), Is.InRange(1, 2));
            });
        }

        [Test]
        public void NoCardUsesAnEnemyOnlyWord_AndNoneOfTheTenPushes()
        {
            // 無防備 is enemy-only (§2.3). Push / pull may sit on a card since v4.3 (§7.3); none of the
            // ten does, and how many will is #160's.
            foreach (var card in CardCatalog.All)
            {
                Assert.That(card.Face.Push, Is.EqualTo(0), card.Id);
                if (card.Trait == null) continue;
                Assert.That(card.Trait.Condition, Is.Not.EqualTo(TraitCondition.Unguarded), card.Id);
            }
        }

        [Test]
        public void ById_FindsACard_AndRefusesAnUnknownId()
        {
            Assert.That(CardCatalog.ById("feint"), Is.SameAs(CardCatalog.Feint));
            Assert.That(() => CardCatalog.ById("nothing"),
                Throws.InstanceOf<System.Collections.Generic.KeyNotFoundException>());
        }

        // ---- The deck (§8) ----

        [Test]
        public void TheDeck_IsTenKindsTimesTwo_AndPassesTheDeckRules()
        {
            var deck = PrototypeDeck.Build();
            var validation = Cards.Validate(deck);

            Assert.Multiple(() =>
            {
                Assert.That(deck, Has.Count.EqualTo(20));
                Assert.That(deck.GroupBy(c => c.Def.Id).All(g => g.Count() == 2), Is.True);
                Assert.That(validation.Ok, Is.True, string.Join(" / ", validation.Errors));
            });
        }

        [Test]
        public void AHandWithNoSwitchCard_IsRarerThanOneInFour()
        {
            // §13 / R102: the chance that 5 cards out of the deck hold no move face stays at or
            // under 25%. Hypergeometric: C(nonMovers, 5) / C(deck, 5).
            var deck = PrototypeDeck.Build();
            int movers = deck.Count(c => c.Def.Attributes.HasFlag(BattleAttribute.Move));
            double chance = Choose(deck.Count - movers, 5) / Choose(deck.Count, 5);

            Assert.That(movers, Is.EqualTo(8));
            Assert.That(chance, Is.LessThanOrEqualTo(0.25));
        }

        // ---- The new words this deck brought in ----

        [Test]
        public void HeavyBlow_AddsSixNow_AndTakesOneRecoveryNextTurn()
        {
            var near = Traits.Evaluate(CardCatalog.BodyCheck.Trait, new TraitContext(Gap: 0));
            var far = Traits.Evaluate(CardCatalog.BodyCheck.Trait, new TraitContext(Gap: 1));

            Assert.Multiple(() =>
            {
                Assert.That(near.PowerBonus, Is.EqualTo(6));
                Assert.That(near.NextTurnRecoveryBonus, Is.EqualTo(-1));
                Assert.That(Combat.RecoverStamina(4, 10, Constants.StaminaRecovery, near.NextTurnRecoveryBonus), Is.EqualTo(6));
                Assert.That(far.Triggered, Is.False);
            });
        }

        [Test]
        public void AMove_GoesAsFarAsTheLineAllows_AndStaysPutAtTheEdge()
        {
            // Player on 2, enemy on 5 (gap 2): the feint steps back to 1; from 1 it cannot step back
            // at all; the boar rush closes two to 4; a plain thrust moves nobody.
            var state = TurnLoop.Start(BattleSetup.Slice(), new FixedRng(0.5)).State;
            Assert.Multiple(() =>
            {
                Assert.That(Field.Move(state, Actor.Player, CardCatalog.Feint.Face.Move), Is.EqualTo(new Shift(2, 1, 0)));
                var atEdge = state with { Player = state.Player with { Cell = 1 } };
                Assert.That(Field.Move(atEdge, Actor.Player, CardCatalog.Feint.Face.Move), Is.EqualTo(new Shift(1, 1, 1)));
                Assert.That(Field.Move(state, Actor.Player, CardCatalog.BoarRush.Face.Move), Is.EqualTo(new Shift(2, 4, 0)));
                Assert.That(Field.Move(state, Actor.Player, CardCatalog.Thrust.Face.Move), Is.EqualTo(new Shift(2, 2, 0)));
            });
        }

        [Test]
        public void BoarRush_ReadsTheGapFromBeforeTheMove()
        {
            // §2.2: played at gap 2 it is 14 + 6, and only then does the player close in.
            var outcome = Traits.Evaluate(CardCatalog.BoarRush.Trait, new TraitContext(Gap: 2));
            Assert.That(Combat.ComputeRawPower(CardCatalog.BoarRush.Face.Power, outcome.PowerBonus), Is.EqualTo(20));
        }

        private static double Choose(int n, int k)
        {
            if (k > n) return 0;
            double result = 1;
            for (int i = 1; i <= k; i++) result = result * (n - k + i) / i;
            return result;
        }
    }
}
