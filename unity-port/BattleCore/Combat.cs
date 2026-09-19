using System;
using System.Collections.Generic;

namespace BattleCore
{
    public static class Combat
    {
        public static int RangeToIndex(RangeBand band)
        {
            IReadOnlyList<RangeBand> order = Constants.RangeOrder;
            for (int i = 0; i < order.Count; i++)
            {
                if (order[i] == band) return i;
            }
            return -1;
        }

        public static int ClampDistance(int index)
        {
            int max = Constants.RangeOrder.Count - 1;
            if (index < 0) return 0;
            return index > max ? max : index;
        }

        public static RangeBand IndexToRange(int index) => Constants.RangeOrder[ClampDistance(index)];

        public static int ShiftDistance(int currentIndex, int shift) => ClampDistance(currentIndex + shift);

        public static int StaminaRecovery(RangeBand band) => Constants.StaminaRecovery[band];

        /// <summary>|distance − effective range| (§2.2).</summary>
        public static int RangeDiff(int distanceIndex, RangeBand effRange)
        {
            return Math.Abs(ClampDistance(distanceIndex) - RangeToIndex(effRange));
        }

        public static double RangeMultiplier(int distanceIndex, RangeBand effRange)
        {
            int idx = Math.Min(RangeDiff(distanceIndex, effRange), Constants.RangeMult.Count - 1);
            return Constants.RangeMult[idx];
        }

        /// <summary>
        /// §7.3: raw = round(power × rangeMult × (desperate ? 1.5 : 1)). Rounding is
        /// away-from-zero (2.5 → 3), the JS Math.round convention the v2 port used;
        /// banker's rounding would make 5 × 0.5 = 2 and 7 × 0.5 = 4, which is not
        /// what the design tables read as. Decided 2026-09-12 (UI spec §9-5).
        /// </summary>
        public static int ComputeAttackDamage(int power, RangeBand effRange, int distanceIndex, bool desperate = false)
        {
            double raw = power * RangeMultiplier(distanceIndex, effRange);
            if (desperate) raw *= Constants.DesperateMult;
            return Math.Max(0, (int)Math.Round(raw, MidpointRounding.AwayFromZero));
        }

        /// <summary>§4: damage = max(0, raw − guard); guard = max(0, guard − raw).</summary>
        public static (int Damage, int GuardAfter, int Absorbed) ApplyGuard(int raw, int guard)
        {
            int damage = Math.Max(0, raw - guard);
            int guardAfter = Math.Max(0, guard - raw);
            return (damage, guardAfter, raw - damage);
        }

        /// <summary>構え (§3.5): Guard +2 when stamina left at turn end ≥ 3, for both sides.</summary>
        public static int ReserveGuard(int staminaLeft)
        {
            return staminaLeft >= Constants.ReserveThreshold ? Constants.ReserveGuard : 0;
        }

        public static bool IsDesperate(CardDef card, int currentStamina)
        {
            return card.Reserve != null
                   && card.Reserve.Kind == ReserveKind.Desperate
                   && currentStamina <= Constants.DesperateThreshold;
        }

        public static bool CalmTriggers(CardDef card, int staminaAfterUse)
        {
            return card.Reserve != null
                   && card.Reserve.Kind == ReserveKind.Calm
                   && staminaAfterUse >= card.Reserve.Threshold;
        }

        /// <summary>§3.1: −1 max stamina per 20% of miasma, capped at −4. 100% is miasma death and never reaches a battle.</summary>
        public static int MiasmaStaminaPenalty(int miasmaPercent)
        {
            int clamped = Math.Max(0, Math.Min(99, miasmaPercent));
            return Math.Min(Constants.MiasmaMaxPenalty, clamped / Constants.MiasmaStepPercent);
        }

        /// <summary>§3.1: max = clamp(10 + clamp(temp, ±4) − miasma penalty, 3..14).</summary>
        public static int ComputeMaxStamina(int tempModifierSum, int miasmaPercent)
        {
            int temp = Math.Max(-Constants.TempModClamp, Math.Min(Constants.TempModClamp, tempModifierSum));
            int raw = Constants.BaseMaxStamina + temp - MiasmaStaminaPenalty(miasmaPercent);
            return Math.Max(Constants.MaxStaminaFloor, Math.Min(Constants.MaxStaminaCeil, raw));
        }

        /// <summary>
        /// Invest choice shared by the enemy AI (§6.3) and the hand's default chip (UI §3.2):
        /// the largest invest in [min..3] that leaves ≥ reserve stamina; otherwise the minimum if
        /// affordable; otherwise null.
        /// </summary>
        public static int? ChooseInvest(int minInvest, int currentStamina, int reserve = Constants.EnemyReserve)
        {
            for (int invest = Constants.MaxInvest; invest >= minInvest; invest--)
            {
                if (currentStamina - invest >= reserve) return invest;
            }
            return currentStamina >= minInvest ? minInvest : (int?)null;
        }
    }
}
