using System;

namespace BattleCore
{
    /// <summary>
    /// The arithmetic of one exchange: §5.1 の damage formula, Guard, 構え, and the two clamps the
    /// turn loop needs. Nothing here holds state — the turn loop (#72) does.
    /// </summary>
    public static class Combat
    {
        /// <summary>
        /// §5.1 / §17.6 F4: add first, then multiply.
        ///
        ///   (face + trait + followUp + conversion) × 強化 × 脆化 → round AwayFromZero → − Guard
        ///
        /// Rounding is away from zero (2.5 → 3), not banker's, because the design tables are read
        /// that way (decided 2026-09-12). This returns the raw power, before Guard; feed it to
        /// <see cref="ApplyGuard"/>.
        ///
        /// The slice always passes 0 for followUp and conversion and 1.0 for both multipliers: 追撃,
        /// 転換, 強化 and 脆化 are #48. The slots stay so that adding them does not reshape the call.
        /// </summary>
        public static int ComputeRawPower(
            int face,
            int traitBonus = 0,
            int followUp = 0,
            int conversion = 0,
            double empowerMult = 1.0,
            double fragileMult = 1.0)
        {
            double raw = (face + traitBonus + followUp + conversion) * empowerMult * fragileMult;
            return Math.Max(0, (int)Math.Round(raw, MidpointRounding.AwayFromZero));
        }

        /// <summary>
        /// Guard soaks the hit before HP does: damage = max(0, raw − guard), and the Guard that
        /// absorbed it is spent. Overkill past the Guard is not refunded to the Guard.
        /// </summary>
        public static (int Damage, int GuardAfter, int Absorbed) ApplyGuard(int raw, int guard)
        {
            int damage = Math.Max(0, raw - guard);
            int guardAfter = Math.Max(0, guard - raw);
            return (damage, guardAfter, raw - damage);
        }

        /// <summary>構え (§9 step 7): Guard +3 when 3 or more stamina is left at turn end, for both sides.</summary>
        public static int ReserveGuard(int staminaLeft) =>
            staminaLeft >= Constants.ReserveThreshold ? Constants.ReserveGuard : 0;

        /// <summary>§3: a card can only be played if its cost is payable in full.</summary>
        public static bool CanPay(int cost, int stamina) => stamina >= cost;

        /// <summary>
        /// §9 steps 2 and 9: recover, then cap at the maximum. The bonus is what 温存 left behind on
        /// the previous turn; 疲労 would subtract here, and is #48.
        /// </summary>
        public static int RecoverStamina(int current, int max, int recovery, int bonus = 0)
        {
            int raw = current + Math.Max(0, recovery + bonus);
            return Math.Min(max, Math.Max(0, raw));
        }

        /// <summary>§8: draw 5 plus whatever modifiers apply, clamped to 3..8.</summary>
        public static int DrawCount(int modifier = 0)
        {
            int raw = Constants.HandDraw + modifier;
            if (raw < Constants.HandDrawMin) return Constants.HandDrawMin;
            return raw > Constants.HandDrawMax ? Constants.HandDrawMax : raw;
        }

        /// <summary>§1: max stamina stays within 3..14 for the whole battle.</summary>
        public static int ClampMaxStamina(int value)
        {
            if (value < Constants.MaxStaminaFloor) return Constants.MaxStaminaFloor;
            return value > Constants.MaxStaminaCeil ? Constants.MaxStaminaCeil : value;
        }

        /// <summary>§17.6 F9: a combatant falls the moment its HP reaches 0, wherever the damage came from.</summary>
        public static bool IsDefeated(int hp) => hp <= 0;
    }
}
