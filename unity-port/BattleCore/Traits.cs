using System;

namespace BattleCore
{
    /// <summary>
    /// What the board looked like when a card or action was played, as far as a trait can see it.
    ///
    /// SelfPosition and OpponentPosition are null for a combatant that carries no position — the
    /// polearm does not (§7.1) — and a position condition never fires against a null side.
    /// StaminaAfterUse is the stamina left once the cost is paid, which is what 温存 reads.
    /// </summary>
    public sealed record TraitContext(
        Position? SelfPosition = null,
        Position? OpponentPosition = null,
        int OpponentGuard = 0,
        int StaminaAfterUse = 0);

    /// <summary>The bonuses one trait contributed. All zero when the condition did not hold.</summary>
    public sealed record TraitOutcome(
        bool Triggered,
        int PowerBonus = 0,
        int GuardBonus = 0,
        int NextTurnRecoveryBonus = 0)
    {
        public static readonly TraitOutcome None = new TraitOutcome(false);
    }

    /// <summary>
    /// §2.3 / §17.6 F5: the trait is judged once, before any face resolves, because the card tables
    /// and the predicted numbers on screen are written for that order.
    ///
    /// The vocabulary is deliberately small (four conditions, three effects). Widening it to the
    /// full 12 × 10 (#48) means adding enum rows and switch arms here — not a different evaluator.
    /// </summary>
    public static class Traits
    {
        public static TraitOutcome Evaluate(Trait? trait, TraitContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (trait == null) return TraitOutcome.None;
            return Holds(trait, context) ? Apply(trait) : TraitOutcome.None;
        }

        /// <summary>§2.3: a position condition reads the side from before the card is played.</summary>
        private static bool Holds(Trait trait, TraitContext context) => trait.Condition switch
        {
            TraitCondition.SelfPosition =>
                context.SelfPosition.HasValue
                && trait.ConditionPosition.HasValue
                && context.SelfPosition.Value == trait.ConditionPosition.Value,

            TraitCondition.OpponentPosition =>
                context.OpponentPosition.HasValue
                && trait.ConditionPosition.HasValue
                && context.OpponentPosition.Value == trait.ConditionPosition.Value,

            TraitCondition.Unguarded => context.OpponentGuard == 0,

            TraitCondition.Reserve => context.StaminaAfterUse >= trait.Threshold,

            _ => throw new ArgumentOutOfRangeException(
                nameof(trait), trait.Condition, "Unknown trait condition."),
        };

        private static TraitOutcome Apply(Trait trait) => trait.Effect switch
        {
            TraitEffect.PowerBonus => new TraitOutcome(true, PowerBonus: trait.Amount),
            TraitEffect.GuardBonus => new TraitOutcome(true, GuardBonus: trait.Amount),
            TraitEffect.NextTurnRecovery => new TraitOutcome(true, NextTurnRecoveryBonus: trait.Amount),
            _ => throw new ArgumentOutOfRangeException(
                nameof(trait), trait.Effect, "Unknown trait effect."),
        };
    }
}
