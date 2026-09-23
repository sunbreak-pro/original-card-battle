using System;
using System.Collections.Generic;

namespace BattleCore
{
    /// <summary>
    /// What the board looked like when a card or action was played, as far as a trait can see it.
    ///
    /// Gap is the distance N to the opponent before the card resolves (§2.3: 出す前の N).
    /// StaminaAfterUse is the stamina left once the cost is paid, which is what 温存 reads;
    /// StaminaBefore is the stamina before paying, which is what 死力 reads.
    ///
    /// #188 widened it to the twelve conditions: Played is what the one playing has played earlier
    /// this turn, in order (連動 / 初手 / 締め / 連打); HandAfterPlay is the hand left once the card
    /// has left it (手薄); OpponentOmen is the standing omen of the enemy the card reads (予兆);
    /// the two status sets are for 相手の状態 / 自分の状態; Attributes are the card's own, which 連打
    /// holds against the card before it. The defaults of the fields #188 added make their conditions
    /// fail (死力, 崩し後, 手薄, 予兆, the statuses, 連動, 連打); the first three keep the slice's
    /// zeros, so an empty context still reads gap 0 (間合い n 以下), Guard 0 (無防備) and nothing
    /// played yet (初手).
    /// </summary>
    public sealed record TraitContext(
        int Gap = 0,
        int OpponentGuard = 0,
        int StaminaAfterUse = 0,
        int StaminaBefore = int.MaxValue,
        int OpponentStamina = int.MaxValue,
        IReadOnlyList<BattleAttribute>? Played = null,
        int HandAfterPlay = int.MaxValue,
        OmenKind? OpponentOmen = null,
        StatusSet? SelfStatuses = null,
        StatusSet? OpponentStatuses = null,
        BattleAttribute Attributes = BattleAttribute.None);

    /// <summary>
    /// The bonuses the traits of one card or action contributed. All zero when no condition held.
    /// Grants are the statuses the Status effect gives; Convert is 転換 (worked out by the turn loop,
    /// which knows the Guard face).
    /// </summary>
    public sealed record TraitOutcome(
        bool Triggered,
        int PowerBonus = 0,
        int GuardBonus = 0,
        int NextTurnRecoveryBonus = 0,
        int StaminaGain = 0,
        int Draw = 0,
        IReadOnlyList<StatusGrant>? Grants = null,
        int CostDown = 0,
        bool Convert = false,
        int FollowUp = 0,
        int BreakBonus = 0)
    {
        public static readonly TraitOutcome None = new TraitOutcome(false);

        public IReadOnlyList<StatusGrant> GrantList => Grants ?? Array.Empty<StatusGrant>();

        /// <summary>Two outcomes added up (背水の陣 carries two traits, both judged at once).</summary>
        public TraitOutcome Plus(TraitOutcome other)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));
            var grants = new List<StatusGrant>(GrantList);
            grants.AddRange(other.GrantList);
            return new TraitOutcome(
                Triggered || other.Triggered,
                PowerBonus + other.PowerBonus,
                GuardBonus + other.GuardBonus,
                NextTurnRecoveryBonus + other.NextTurnRecoveryBonus,
                StaminaGain + other.StaminaGain,
                Draw + other.Draw,
                grants.Count == 0 ? null : grants,
                CostDown + other.CostDown,
                Convert || other.Convert,
                FollowUp + other.FollowUp,
                BreakBonus + other.BreakBonus);
        }
    }

    /// <summary>
    /// §2.3 / §17.6 F5: the trait is judged once, before any face resolves, because the card tables
    /// and the predicted numbers on screen are written for that order.
    ///
    /// The twelve conditions and ten effects (#188) are rows of the two switches below; the demo
    /// carries them at the precision a playable battle needs, and #48 keeps the tests that pin every
    /// pairing.
    /// </summary>
    public static class Traits
    {
        public static TraitOutcome Evaluate(Trait? trait, TraitContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (trait == null) return TraitOutcome.None;
            return Holds(trait, context) ? Apply(trait) : TraitOutcome.None;
        }

        /// <summary>Every trait of one card, judged on the same board; the outcomes add up.</summary>
        public static TraitOutcome EvaluateAll(IReadOnlyList<Trait> traits, TraitContext context)
        {
            if (traits == null) throw new ArgumentNullException(nameof(traits));
            var total = TraitOutcome.None;
            foreach (var trait in traits) total = total.Plus(Evaluate(trait, context));
            return total;
        }

        /// <summary>§2.3: a gap condition reads N from before the card is played (before any move face).</summary>
        public static bool Holds(Trait trait, TraitContext context)
        {
            if (trait == null) throw new ArgumentNullException(nameof(trait));
            if (context == null) throw new ArgumentNullException(nameof(context));
            var played = context.Played ?? Array.Empty<BattleAttribute>();
            switch (trait.Condition)
            {
                case TraitCondition.GapAtMost: return context.Gap <= trait.Threshold;
                case TraitCondition.GapAtLeast: return context.Gap >= trait.Threshold;
                case TraitCondition.Unguarded: return context.OpponentGuard == 0;
                case TraitCondition.Reserve: return context.StaminaAfterUse >= trait.Threshold;
                case TraitCondition.Desperate: return context.StaminaBefore <= Constants.DesperateThreshold;
                case TraitCondition.FirstPlay: return played.Count == 0;
                case TraitCondition.Finisher: return played.Count + 1 >= Constants.FinisherPlayNumber;
                case TraitCondition.Broken: return context.OpponentStamina < Constants.BrokenBelow;
                case TraitCondition.Thin: return context.HandAfterPlay <= Constants.ThinHandMax;
                case TraitCondition.OmenIs: return trait.Omen.HasValue && context.OpponentOmen == trait.Omen;
                case TraitCondition.FoeHas:
                    return trait.Watch.HasValue && context.OpponentStatuses != null && context.OpponentStatuses.Has(trait.Watch.Value);
                case TraitCondition.SelfHas:
                    return trait.Watch.HasValue && context.SelfStatuses != null && context.SelfStatuses.Has(trait.Watch.Value);
                case TraitCondition.Combo:
                    foreach (var attributes in played)
                    {
                        if ((attributes & trait.Attribute) != 0) return true;
                    }
                    return false;
                case TraitCondition.Chain:
                    return played.Count > 0 && (played[played.Count - 1] & context.Attributes) != 0;
                default:
                    throw new ArgumentOutOfRangeException(nameof(trait), trait.Condition, "Unknown trait condition.");
            }
        }

        private static TraitOutcome Apply(Trait trait) => trait.Effect switch
        {
            TraitEffect.PowerBonus => new TraitOutcome(true, PowerBonus: trait.Amount),
            TraitEffect.GuardBonus => new TraitOutcome(true, GuardBonus: trait.Amount),
            TraitEffect.NextTurnRecovery => new TraitOutcome(true, NextTurnRecoveryBonus: trait.Amount),
            TraitEffect.HeavyBlow => new TraitOutcome(
                true,
                PowerBonus: Constants.HeavyBlowPower,
                NextTurnRecoveryBonus: -Constants.HeavyBlowRecoveryPenalty),
            TraitEffect.StaminaGain => new TraitOutcome(true, StaminaGain: trait.Amount),
            TraitEffect.Draw => new TraitOutcome(true, Draw: trait.Amount),
            TraitEffect.Status => new TraitOutcome(
                true, Grants: trait.Grant != null ? new[] { trait.Grant } : null),
            TraitEffect.CostDown => new TraitOutcome(true, CostDown: trait.Amount),
            TraitEffect.Convert => new TraitOutcome(true, Convert: true),
            TraitEffect.FollowUp => new TraitOutcome(true, FollowUp: trait.Amount),
            TraitEffect.BreakBonus => new TraitOutcome(true, BreakBonus: trait.Amount),
            _ => throw new ArgumentOutOfRangeException(
                nameof(trait), trait.Effect, "Unknown trait effect."),
        };
    }
}
