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

                Assert.That(CardCatalog.KesaCut.Trait, Is.EqualTo(
                    new Trait(TraitCondition.SelfPosition, TraitEffect.PowerBonus, 5, Position.Near)));
                Assert.That(CardCatalog.ReachThrust.Trait, Is.EqualTo(
                    new Trait(TraitCondition.SelfPosition, TraitEffect.PowerBonus, 5, Position.Far)));
                Assert.That(CardCatalog.BoarRush.Trait, Is.EqualTo(
                    new Trait(TraitCondition.SelfPosition, TraitEffect.PowerBonus, 6, Position.Far)));
                Assert.That(CardCatalog.ShieldBash.Trait, Is.EqualTo(
                    new Trait(TraitCondition.SelfPosition, TraitEffect.GuardBonus, 3, Position.Near)));
                Assert.That(CardCatalog.BodyCheck.Trait, Is.EqualTo(
                    new Trait(TraitCondition.SelfPosition, TraitEffect.HeavyBlow, 0, Position.Near)));
                Assert.That(CardCatalog.Brace.Trait, Is.EqualTo(
                    new Trait(TraitCondition.Reserve, TraitEffect.NextTurnRecovery, 1, null, 6)));
                Assert.That(CardCatalog.Feint.Trait, Is.EqualTo(
                    new Trait(TraitCondition.Reserve, TraitEffect.GuardBonus, 3, null, 4)));
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
            Assert.Multiple(() =>
            {
                Assert.That(CardCatalog.Feint.Face.MoveTo, Is.EqualTo(Position.Far));
                Assert.That(CardCatalog.StepOutGuard.Face.MoveTo, Is.EqualTo(Position.Far));
                Assert.That(CardCatalog.BoarRush.Face.MoveTo, Is.EqualTo(Position.Near));
                Assert.That(CardCatalog.StepInGuard.Face.MoveTo, Is.EqualTo(Position.Near));
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
                Assert.That(all.Count(c => c.Face.MoveTo == Position.Near), Is.GreaterThanOrEqualTo(1));
                Assert.That(all.Count(c => c.Face.MoveTo == Position.Far), Is.GreaterThanOrEqualTo(1));
                Assert.That(all.Count(c => c.Face.Status == StatusKind.Slow), Is.InRange(1, 2));
            });
        }

        [Test]
        public void NoCardReadsTheOpponentSide_OrUsesAnEnemyOnlyWord()
        {
            // §2.3: a player card reads its own position only. 無防備 and push are enemy-only.
            foreach (var card in CardCatalog.All)
            {
                Assert.That(card.Face.Push, Is.False, card.Id);
                if (card.Trait == null) continue;
                Assert.That(card.Trait.Condition, Is.Not.EqualTo(TraitCondition.OpponentPosition), card.Id);
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
            var near = Traits.Evaluate(CardCatalog.BodyCheck.Trait, new TraitContext(SelfPosition: Position.Near));
            var far = Traits.Evaluate(CardCatalog.BodyCheck.Trait, new TraitContext(SelfPosition: Position.Far));

            Assert.Multiple(() =>
            {
                Assert.That(near.PowerBonus, Is.EqualTo(6));
                Assert.That(near.NextTurnRecoveryBonus, Is.EqualTo(-1));
                Assert.That(Combat.RecoverStamina(4, 10, Constants.StaminaRecovery, near.NextTurnRecoveryBonus), Is.EqualTo(6));
                Assert.That(far.Triggered, Is.False);
            });
        }

        [Test]
        public void ADirectedMove_LandsOnItsSide_AndStaysPutWhenAlreadyThere()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Combat.MoveResult(Position.Near, CardCatalog.Feint.Face), Is.EqualTo(Position.Far));
                Assert.That(Combat.MoveResult(Position.Far, CardCatalog.Feint.Face), Is.EqualTo(Position.Far));
                Assert.That(Combat.MoveResult(Position.Far, CardCatalog.BoarRush.Face), Is.EqualTo(Position.Near));
                Assert.That(Combat.MoveResult(Position.Near, new Face(FlipsSelfPosition: true)), Is.EqualTo(Position.Far));
                Assert.That(Combat.MoveResult(Position.Near, CardCatalog.Thrust.Face), Is.EqualTo(Position.Near));
                Assert.That(Combat.MoveResult(null, CardCatalog.Feint.Face), Is.Null);
            });
        }

        [Test]
        public void BoarRush_ReadsTheSideFromBeforeTheMove()
        {
            // §2.2: played from far it is 14 + 6, and only then does the player land near.
            var outcome = Traits.Evaluate(CardCatalog.BoarRush.Trait, new TraitContext(SelfPosition: Position.Far));
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
