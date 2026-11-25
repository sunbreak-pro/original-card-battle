using System;

namespace DungeonCore
{
    /// <summary>
    /// Max stamina and field recovery on the exploration side. The 瘴気 gauge that used to lower max stamina left
    /// the exploration (concept-v3.md §6, #888), so only the temporary modifiers move the maximum now.
    /// </summary>
    public static class StaminaRules
    {
        /// <summary>Base max stamina before any modifier (battle_core_v4.md §1).</summary>
        public const int BaseMaxStamina = 10;

        public const int MaxStaminaFloor = 3;
        public const int MaxStaminaCeil = 14;

        /// <summary>Temporary modifiers from exploration clamp at ±4 (battle_core_v3.md §3.1).</summary>
        public const int TempModClamp = 4;

        /// <summary>Stamina recovered outside battle, once per new node (concept-v3.md §12-16).</summary>
        public const int FieldRecovery = 3;

        /// <summary>max = clamp(10 + clamp(temp, ±4), 3..14).</summary>
        public static int MaxStamina(int tempModifierSum)
        {
            int temp = ClampTemp(tempModifierSum);
            return Math.Max(MaxStaminaFloor, Math.Min(MaxStaminaCeil, BaseMaxStamina + temp));
        }

        /// <summary>A sum of temporary modifiers, held inside ±<see cref="TempModClamp"/>.</summary>
        public static int ClampTemp(int tempModifierSum) =>
            Math.Max(-TempModClamp, Math.Min(TempModClamp, tempModifierSum));
    }
}
