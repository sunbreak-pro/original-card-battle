using System.Collections.Generic;

namespace BattleCore
{
    /// <summary>
    /// Numbers from battle_core_v4.md §10, the single source of truth for values. Only the rows the
    /// vertical slice reads are here; the rows it does not read (owned-kind cap, trait vocabulary
    /// sizes, stance slots, elite counts, mastery thresholds, chain battles, and the v4.3 rows for
    /// several enemies: GAP_SCALING_CARDS_MAX) come back with #47 / #52. REACH_DEFAULT lives on
    /// <see cref="Reach.Default"/>. FIELD_CELLS has no default any more (2026-09-23, #169): every
    /// battle is handed its width (<see cref="BattleSetup.FieldCells"/>).
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

        // ---- Cells and gap (§7, v4.3) ----

        /// <summary>
        /// §7.1: the widths a battle may be handed. The width itself is not a constant (2026-09-23,
        /// #169): the layer and the battle decide it, and the dungeon side picks the values (#168).
        /// </summary>
        public const int FieldCellsMin = 5;

        public const int FieldCellsMax = 8;

        /// <summary>§7.1 `CELL_CAPACITY`: one enemy per cell (2026-09-23). The player never shares a cell.</summary>
        public const int CellCapacity = 1;

        /// <summary>§7.4 `ENEMIES_MAX`: enemies on the field at once, each on its own cell.</summary>
        public const int EnemiesMax = 3;

        /// <summary>§7.1: the cells a large enemy may use.</summary>
        public const int EnemySizeMax = 3;

        /// <summary>§7.3: the player starts on cell 2, one cell of room behind.</summary>
        public const int PlayerStartCell = 2;

        /// <summary>
        /// §7.3 `START_GAP`: the opening gap (3 since 2026-09-23, #169). The enemy's near edge starts
        /// this many empty cells to the right of the player; see <see cref="Field.EnemyStartCell"/>
        /// for a line too short to hold it.
        /// </summary>
        public const int StartGap = 3;

        /// <summary>§7.3: a move face, a push or a pull covers at most 2 cells at once.</summary>
        public const int MoveStepMax = 2;

        /// <summary>§7.3: damage per cell a pushed side could not move (叩き台). Guard reduces it.</summary>
        public const int WallDamage = 3;

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
