using System;

namespace DungeonCore
{
    /// <summary>
    /// 瘴気: how far one life can be pushed. Every 刻限 spent inside a layer adds that
    /// layer's density to the gauge; every full 20% costs one point of max stamina; 100%
    /// ends the life (concept-v3.md §6).
    ///
    /// This is the exploration side of the gauge only. The boss state 瘴気纏い lowers max
    /// stamina inside a battle and is restored when the battle ends — it never touches this
    /// gauge (battle_core_v4.md §6.2).
    /// </summary>
    public static class Miasma
    {
        /// <summary>The gauge runs 0..100. Reaching 100 is 瘴気死.</summary>
        public const int DeathAt = 100;

        /// <summary>One point of max stamina per full 20% (concept-v3.md §6).</summary>
        public const int StepPercent = 20;

        /// <summary>The penalty stops at −4, which is max stamina 6 at 80% (battle_core_v4.md §13).</summary>
        public const int MaxPenalty = 4;

        /// <summary>Relief can thin the air but never clear it: a 刻限 always costs at least 1%.</summary>
        public const int DensityFloor = 1;

        /// <summary>Base max stamina before any modifier (battle_core_v4.md §1).</summary>
        public const int BaseMaxStamina = 10;

        public const int MaxStaminaFloor = 3;
        public const int MaxStaminaCeil = 14;

        /// <summary>Temporary modifiers from exploration clamp at ±4 (battle_core_v3.md §3.1).</summary>
        public const int TempModClamp = 4;

        /// <summary>Stamina recovered outside battle, once per 刻限 spent (concept-v3.md §12-16).</summary>
        public const int FieldRecovery = 3;

        /// <summary>A layer's density after the run's relief is applied, floored at 1.</summary>
        public static int EffectiveDensity(int layerDensity, int densityRelief)
        {
            if (layerDensity < 0) throw new ArgumentOutOfRangeException(nameof(layerDensity));
            return Math.Max(DensityFloor, layerDensity - Math.Max(0, densityRelief));
        }

        /// <summary>Adds one 刻限 worth of miasma, clamped to the lethal ceiling.</summary>
        public static int Accumulate(int percent, int effectiveDensity) =>
            Math.Min(DeathAt, Math.Max(0, percent) + Math.Max(0, effectiveDensity));

        /// <summary>Consumables and the like pull the gauge back down, never below zero.</summary>
        public static int Relieve(int percent, int amount) =>
            Math.Max(0, Math.Max(0, percent) - Math.Max(0, amount));

        public static bool IsLethal(int percent) => percent >= DeathAt;

        /// <summary>
        /// −1 max stamina per full 20%, capped at −4. 100% never reaches a battle because the
        /// life has already ended, so the reading is clamped to 99 the way the battle core does.
        /// </summary>
        public static int MaxStaminaPenalty(int percent)
        {
            int clamped = Math.Max(0, Math.Min(DeathAt - 1, percent));
            return Math.Min(MaxPenalty, clamped / StepPercent);
        }

        /// <summary>max = clamp(10 + clamp(temp, ±4) − miasma penalty, 3..14).</summary>
        public static int MaxStamina(int tempModifierSum, int percent)
        {
            int temp = Math.Max(-TempModClamp, Math.Min(TempModClamp, tempModifierSum));
            int raw = BaseMaxStamina + temp - MaxStaminaPenalty(percent);
            return Math.Max(MaxStaminaFloor, Math.Min(MaxStaminaCeil, raw));
        }
    }
}
