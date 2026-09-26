using System.Collections.Generic;

namespace BattleCore
{
    /// <summary>
    /// Numbers from battle_core_v4.md §10, the single source of truth for values. Every row of the
    /// table is here (#47), including the ones the core does not read yet — they are the authoring
    /// limits the card and enemy data are checked against, and the numbers #48〜#53 read.
    /// REACH_DEFAULT lives on <see cref="Reach.Default"/> and DUAL_FACE_RATIO on
    /// <see cref="Columns.DualFaceRatio"/>. FIELD_CELLS has no default any more (2026-09-23, #169):
    /// every battle is handed its width (<see cref="BattleSetup.FieldCells"/>).
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

        /// <summary>
        /// §8 (§19.6 S15): cards with a stance face in one deck, all kinds counted together. A limit
        /// of its own, apart from the three of a kind.
        /// </summary>
        public const int StanceCardsMax = 3;

        /// <summary>§8: the kinds one character may own (40 to start, 40 learned).</summary>
        public const int OwnedKindsMax = 80;

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

        /// <summary>死力: the condition's stamina bar. It carries no multiplier any more (DESPERATE_MULT is gone).</summary>
        public const int DesperateThreshold = 2;

        // ---- Traits (§2.3) ----

        /// <summary>重撃: power +6 on the card, and the next turn start recovers 1 less.</summary>
        public const int HeavyBlowPower = 6;

        public const int HeavyBlowRecoveryPenalty = 1;

        /// <summary>§2.3: the trait vocabulary — 12 conditions × 10 effects.</summary>
        public const int TraitConditions = 12;

        public const int TraitEffects = 10;

        /// <summary>§10: cards whose condition is a gap threshold (4 cells of the grid × 4).</summary>
        public const int PositionTraitCards = 16;

        /// <summary>§7.2: at most 4 of the 80 kinds may scale with N ("N 1 につき +x").</summary>
        public const int GapScalingCardsMax = 4;

        /// <summary>§3.1: a card with no trait takes +2 on every column (素直な札).</summary>
        public const int PlainCardBonus = 2;

        /// <summary>§2.3: the same condition + effect pair appears on at most 3 kinds.</summary>
        public const int SameTraitMax = 3;

        /// <summary>
        /// §2.3 手薄: the hand left after the play holds at most this many cards. The canon has 2
        /// (§2.3, §19.2 S3) and the card and enemy tables 1; 2 was decided on 2026-09-23 (#193).
        /// </summary>
        public const int ThinHandMax = 2;

        /// <summary>§2.3 締め: the play is the third of the turn or later.</summary>
        public const int FinisherPlayNumber = 3;

        /// <summary>§2.3 崩し後: the opponent's stamina is below this.</summary>
        public const int BrokenBelow = 3;

        /// <summary>§2.3 追撃: the next attack face this turn gets this much power.</summary>
        public const int FollowUpPower = 5;

        // ---- Status (§5) ----

        /// <summary>§5: the player may hold six kinds at once. Enemies have no kind cap.</summary>
        public const int StatusKindsPlayer = 6;

        /// <summary>§3.1: applying a status gives 2 stacks unless the face says otherwise.</summary>
        public const int StatusApplyDefault = 2;

        /// <summary>
        /// §5 (#205): a ターンで減る型 word holds at most this many stacks, on either side; whatever an
        /// application would put over it is dropped. Column 4's 出血 / 再生 fits whole. The 使うと減る型
        /// is not counted (§19.5 S14). The battle lane's proposal, pending the owner's confirmation (§23).
        /// </summary>
        public const int TurnDecayStackMax = 4;

        /// <summary>§3.1 / §5: 出血 and 再生 move HP by this much per stack at the holder's turn start.</summary>
        public const int BleedPerStack = 2;

        public const int RegenPerStack = 2;

        /// <summary>§5 威圧: the holder's next action loses this much power and Guard.</summary>
        public const int IntimidatePenalty = 3;

        /// <summary>§5 疲労 / §9 step 2: the holder recovers this much less while it holds the word.</summary>
        public const int FatiguePenalty = 1;

        /// <summary>§4: one stance at a time, for both sides.</summary>
        public const int StanceSlots = 1;

        /// <summary>§5: the boss-only statuses.</summary>
        public const int BossStatusKinds = 2;

        // ---- Elites, bosses, mastery, chains (§6 / §12 / §17.8) ----

        /// <summary>§6: an elite or a boss takes two actions in its phase.</summary>
        public const int EliteActions = 2;

        /// <summary>§6: an elite's omen shows two steps; a normal enemy's shows one.</summary>
        public const int OmenDepthElite = 2;

        /// <summary>§17.8: the mastery steps (placeholder).</summary>
        public static readonly IReadOnlyList<int> MasteryThresholds = new[] { 3, 8, 15 };

        /// <summary>§12: battles in a chain by default (a nine-battle order may be picked too).</summary>
        public const int ChainBattlesDefault = 3;

        /// <summary>§3.1 / §12: the rest between chained battles (階層間の休憩) gives back this share of max HP, and all stamina.</summary>
        public const int ChainRestHpPercent = 30;

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

        /// <summary>§5.1: 強化 and 脆化 both multiply by 1.5, and one blow takes only one of them, 脆化 first (§19.5 S13).</summary>
        public const double EmpowerMult = 1.5;

        public const double FragileMult = 1.5;
    }
}
