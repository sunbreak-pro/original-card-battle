using System.Collections.Generic;

namespace BattleCore
{
    /// <summary>Numbers from battle_core_v3.md §10 (the single source of truth for values).</summary>
    public static class Constants
    {
        public const int BaseMaxStamina = 10;
        public const int MaxStaminaFloor = 3;
        public const int MaxStaminaCeil = 14;
        public const int TempModClamp = 4;
        public const int MiasmaStepPercent = 20;
        public const int MiasmaMaxPenalty = 4;

        public static readonly IReadOnlyDictionary<RangeBand, int> StaminaRecovery =
            new Dictionary<RangeBand, int>
            {
                [RangeBand.Close] = 1,
                [RangeBand.Mid] = 2,
                [RangeBand.Far] = 3,
            };

        public const int FieldRecovery = 3;
        public const int MaxInvest = 3;
        public const int ReserveThreshold = 3;
        public const int ReserveGuard = 2;
        public const int DesperateThreshold = 2;
        public const double DesperateMult = 1.5;

        public static readonly IReadOnlyList<double> RangeMult = new[] { 1.0, 0.5, 0.15 };
        public const int WhiffDiff = 2;

        public static readonly IReadOnlyList<RangeBand> RangeOrder = new[]
        {
            RangeBand.Close,
            RangeBand.Mid,
            RangeBand.Far,
        };

        public const int MidIndex = 1;

        public static readonly IReadOnlyDictionary<RangeBand, string> RangeLabel =
            new Dictionary<RangeBand, string>
            {
                [RangeBand.Close] = "近",
                [RangeBand.Mid] = "中",
                [RangeBand.Far] = "遠",
            };

        public const int PlayerMaxHp = 30;
        public const int HandSize = 3;
        public const int InitialDistanceIndex = MidIndex;
        public const int EnemyReserve = 3;

        /// <summary>Disclosure levels of the enemy's journal page (battle_ui_ux_v1.md §7): 0 = kind only, 1 = kind + range, 2 = name + range + power span.</summary>
        public const int DisclosureMax = 2;
    }
}
