using NUnit.Framework;
using BattleCore;

namespace BattleCore.Tests
{
    /// <summary>
    /// §2.3 / §17.6 F5: the trait is judged once, before any face resolves. These are the words the
    /// polearm needs plus the gap thresholds the player cards use (v4.3: 間合い n 以下 / n 以上).
    /// </summary>
    public class TraitTests
    {
        private static readonly Trait SweepTrait =
            new Trait(TraitCondition.GapAtLeast, TraitEffect.PowerBonus, Amount: 3, Threshold: 2);

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
        public void GapAtLeast_FiresFromTheThresholdUp()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Traits.Evaluate(SweepTrait, new TraitContext(Gap: 2)).PowerBonus, Is.EqualTo(3));
                Assert.That(Traits.Evaluate(SweepTrait, new TraitContext(Gap: 4)).Triggered, Is.True);
                Assert.That(Traits.Evaluate(SweepTrait, new TraitContext(Gap: 1)).Triggered, Is.False);
                Assert.That(Traits.Evaluate(SweepTrait, new TraitContext(Gap: 0)).Triggered, Is.False);
            });
        }

        [Test]
        public void GapAtMost_FiresFromTheThresholdDown()
        {
            var trait = new Trait(TraitCondition.GapAtMost, TraitEffect.GuardBonus, Amount: 3, Threshold: 0);
            Assert.Multiple(() =>
            {
                Assert.That(Traits.Evaluate(trait, new TraitContext(Gap: 0)).GuardBonus, Is.EqualTo(3));
                Assert.That(Traits.Evaluate(trait, new TraitContext(Gap: 1)).Triggered, Is.False);
                Assert.That(Traits.Evaluate(trait with { Threshold = 1 }, new TraitContext(Gap: 1)).Triggered, Is.True);
            });
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
        public void SweepHitsElevenAtGapTwoAndEightAtGapOne()
        {
            // The vertical slice's headline number (#68 の数値 5), read on the gap now.
            int far = Combat.ComputeRawPower(
                face: 8, traitBonus: Traits.Evaluate(SweepTrait, new TraitContext(Gap: 2)).PowerBonus);
            int near = Combat.ComputeRawPower(
                face: 8, traitBonus: Traits.Evaluate(SweepTrait, new TraitContext(Gap: 1)).PowerBonus);

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
