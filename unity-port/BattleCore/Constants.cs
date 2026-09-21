using System.Collections.Generic;

namespace BattleCore
{
    /// <summary>
    /// Numbers from battle_core_v4.md §10, the single source of truth for values. Only the rows the
    /// vertical slice reads are here; the rows it does not read (owned-kind cap, trait vocabulary
    /// sizes, stance slots, elite counts, mastery thresholds, chain battles) come back with #47.
    ///
    /// Gone from v3 and deliberately not replaced: T0_ATTACK_POWER, MIN_INVEST_ZERO_KINDS,
    /// RANGE_MULT, STATUS_SLOTS, STATUS_STACK_MAX and DESPERATE_MULT (§10 廃止した定数). The miasma
    /// max-stamina penalty left with them: it belongs to the exploration layer (#99) and the
    /// in-battle exception is a boss status (#50).
    /// </summary>
    public static class Constants
    {
        // ---- Hand and deck (§8) ----

        public const int HandDraw = 5;
        public const int HandDrawMin = 3;
        public const int HandDrawMax = 8;
        public const int HandLimit = 8;
        public const int DeckMin = 20;
        public const int DeckMax = 40;
        public const int CopiesMax = 3;

        // ---- Cost and columns (§3) ----

        public const int ColumnCount = 4;
        public const int CostMin = 1;
        public const int CostMax = 3;

        // ---- Stamina and HP (§3.1 / §9) ----

        /// <summary>§9 step 2: the player recovers a flat 3 every turn. Enemies use their own value.</summary>
        public const int StaminaRecovery = 3;

        public const int BaseMaxStamina = 10;
        public const int MaxStaminaFloor = 3;
        public const int MaxStaminaCeil = 14;
        public const int PlayerMaxHp = 50;

        /// <summary>構え (§9 step 7): Guard +3 when at least 3 stamina is left at turn end.</summary>
        public const int ReserveThreshold = 3;

        public const int ReserveGuard = 3;

        // ---- Traits (§2.3) ----

        /// <summary>重撃: power +6 on the card, and the next turn start recovers 1 less.</summary>
        public const int HeavyBlowPower = 6;

        public const int HeavyBlowRecoveryPenalty = 1;

        // ---- Status (§5) ----

        /// <summary>§5: the player may hold six kinds at once. Enemies have no cap.</summary>
        public const int StatusKindsPlayer = 6;

        /// <summary>§3.1: applying a status gives 2 stacks unless the face says otherwise.</summary>
        public const int StatusApplyDefault = 2;

        // ---- Resolution order (§2.2 / §17.6 F5) ----

        /// <summary>
        /// The order faces resolve in within one card. The trait is evaluated once before all of
        /// them, so it is not a member of this list.
        /// </summary>
        public static readonly IReadOnlyList<BattleAttribute> FaceOrder = new[]
        {
            BattleAttribute.Attack,
            BattleAttribute.Move,
            BattleAttribute.Guard,
            BattleAttribute.Skill,
            BattleAttribute.Stance,
        };

        // ---- Damage multipliers (§5.1) ----

        /// <summary>強化 and 脆化 both multiply by 1.5. Neither is in the slice; the formula keeps the slot.</summary>
        public const double EmpowerMult = 1.5;

        public const double FragileMult = 1.5;
    }
}
