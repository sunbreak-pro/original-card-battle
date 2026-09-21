using NUnit.Framework;
using BattleCore;

namespace BattleCore.Tests
{
    /// <summary>
    /// §2.3 / §17.6 F5: the trait is judged once, before any face resolves. These are the three
    /// words the polearm needs plus the self-position word the player cards will use.
    /// </summary>
    public class TraitTests
    {
        private static readonly Trait SweepTrait =
            new Trait(TraitCondition.OpponentPosition, TraitEffect.PowerBonus, Amount: 3, ConditionPosition: Position.Far);

        private static readonly Trait ShoveTrait =
            new Trait(TraitCondition.Unguarded, TraitEffect.PowerBonus, Amount: 3);

        private static readonly Trait GuardUpTrait =
            new Trait(TraitCondition.Reserve, TraitEffect.NextTurnRecovery, Amount: 1, Threshold: 4);

        [Test]
        public void NoTrait_ContributesNothing()
        {
            var outcome = Traits.Evaluate(null, new TraitContext());
            Assert.Multiple(() =>
            {
                Assert.That(outcome.Triggered, Is.False);
                Assert.That(outcome.PowerBonus, Is.EqualTo(0));
            });
        }

        [Test]
        public void OpponentPosition_FiresWhenThePlayerStandsOnTheNamedSide()
        {
            var outcome = Traits.Evaluate(SweepTrait, new TraitContext(OpponentPosition: Position.Far));
            Assert.Multiple(() =>
            {
                Assert.That(outcome.Triggered, Is.True);
                Assert.That(outcome.PowerBonus, Is.EqualTo(3));
            });
        }

        [Test]
        public void OpponentPosition_StaysQuietOnTheOtherSide()
        {
            var outcome = Traits.Evaluate(SweepTrait, new TraitContext(OpponentPosition: Position.Near));
            Assert.That(outcome.Triggered, Is.False);
        }

        [Test]
        public void OpponentPosition_StaysQuietWhenThereIsNoSideToRead()
        {
            var outcome = Traits.Evaluate(SweepTrait, new TraitContext(OpponentPosition: null));
            Assert.That(outcome.Triggered, Is.False);
        }

        [Test]
        public void Unguarded_FiresOnlyWhileTheOpponentGuardIsZero()
        {
            Assert.That(Traits.Evaluate(ShoveTrait, new TraitContext(OpponentGuard: 0)).PowerBonus, Is.EqualTo(3));
            Assert.That(Traits.Evaluate(ShoveTrait, new TraitContext(OpponentGuard: 1)).Triggered, Is.False);
        }

        [Test]
        public void Reserve_ReadsTheStaminaLeftAfterTheCostIsPaid()
        {
            Assert.That(Traits.Evaluate(GuardUpTrait, new TraitContext(StaminaAfterUse: 4)).NextTurnRecoveryBonus, Is.EqualTo(1));
            Assert.That(Traits.Evaluate(GuardUpTrait, new TraitContext(StaminaAfterUse: 3)).Triggered, Is.False);
        }

        [Test]
        public void SelfPosition_ReadsThePlayerOwnSide()
        {
            var trait = new Trait(
                TraitCondition.SelfPosition, TraitEffect.GuardBonus, Amount: 3, ConditionPosition: Position.Near);

            Assert.That(Traits.Evaluate(trait, new TraitContext(SelfPosition: Position.Near)).GuardBonus, Is.EqualTo(3));
            Assert.That(Traits.Evaluate(trait, new TraitContext(SelfPosition: Position.Far)).Triggered, Is.False);
        }

        [Test]
        public void SweepHitsElevenFromFarAndEightFromNear()
        {
            // The vertical slice's headline number (#68 の数値 5).
            int far = Combat.ComputeRawPower(
                face: 8,
                traitBonus: Traits.Evaluate(SweepTrait, new TraitContext(OpponentPosition: Position.Far)).PowerBonus);
            int near = Combat.ComputeRawPower(
                face: 8,
                traitBonus: Traits.Evaluate(SweepTrait, new TraitContext(OpponentPosition: Position.Near)).PowerBonus);

            Assert.Multiple(() =>
            {
                Assert.That(far, Is.EqualTo(11));
                Assert.That(near, Is.EqualTo(8));
            });
        }

        [Test]
        public void ShoveHitsEightThroughNoGuardAndFiveThroughSome()
        {
            // #68 の数値 6: 5 + 3 while the player is unguarded, 5 once any Guard is up.
            int bare = Combat.ComputeRawPower(
                face: 5, traitBonus: Traits.Evaluate(ShoveTrait, new TraitContext(OpponentGuard: 0)).PowerBonus);
            int guarded = Combat.ComputeRawPower(
                face: 5, traitBonus: Traits.Evaluate(ShoveTrait, new TraitContext(OpponentGuard: 4)).PowerBonus);

            Assert.Multiple(() =>
            {
                Assert.That(bare, Is.EqualTo(8));
                Assert.That(guarded, Is.EqualTo(5));
                // The Guard then takes its share out of that 5.
                Assert.That(Combat.ApplyGuard(guarded, 4).Damage, Is.EqualTo(1));
            });
        }
    }
}
