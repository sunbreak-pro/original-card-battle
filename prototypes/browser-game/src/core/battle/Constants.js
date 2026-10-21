// Port of unity-port/BattleCore/Constants.cs
//
// This module is a LEAF on purpose: it imports nothing, so every other battle module can read it
// at module-evaluation time (record defaults, scales) without a circular-import TDZ error.
// That is why FaceOrder below is written with the FaceKind member strings ('Attack', ...) instead of
// importing FaceKind from Types.js: a defineEnum value IS its member name, so the values are identical.

/**
 * Numbers from battle_core_v4.md §10, the single source of truth for values. Every row of the
 * table is here (#47), including the ones the core does not read yet — they are the authoring
 * limits the card and enemy data are checked against, and the numbers #48〜#53 read.
 * REACH_DEFAULT lives on Reach.Default and DUAL_FACE_RATIO on Columns.DualFaceRatio.
 * FIELD_CELLS has no default any more (2026-09-23, #169): every battle is handed its width
 * (BattleSetup.FieldCells).
 *
 * Gone from v3 and deliberately not replaced: T0_ATTACK_POWER, MIN_INVEST_ZERO_KINDS,
 * RANGE_MULT, STATUS_SLOTS, STATUS_STACK_MAX and DESPERATE_MULT (§10 廃止した定数). The miasma
 * max-stamina penalty left with them: it belongs to the exploration layer (#99) and the
 * in-battle exception is a boss status (#50).
 */
export const Constants = Object.freeze({
  // ---- Hand and deck (§8) ----

  /** §8: six cards a turn (5 until 2026-10-06, #351). Modifiers clamp to 3..8. */
  HandDraw: 6,
  HandDrawMin: 3,
  HandDrawMax: 8,
  HandLimit: 8,
  DeckMin: 20,
  DeckMax: 40,
  CopiesMax: 3,

  /** §8: the kinds one character may own (40 to start, 40 learned). */
  OwnedKindsMax: 80,

  // ---- Cost and columns (§3) ----

  ColumnCount: 4,
  CostMin: 1,
  CostMax: 3,

  // ---- Stamina and HP (§3.1 / §9) ----

  /** §9 step 2: the player recovers a flat 3 every turn. Enemies use their own value. */
  StaminaRecovery: 3,

  BaseMaxStamina: 10,
  MaxStaminaFloor: 3,
  MaxStaminaCeil: 14,
  PlayerMaxHp: 50,

  /** 構え (§9 step 7): Guard +3 when at least 3 stamina is left at turn end. */
  ReserveThreshold: 3,

  ReserveGuard: 3,

  /** 死力: the condition's stamina bar. It carries no multiplier any more (DESPERATE_MULT is gone). */
  DesperateThreshold: 2,

  // ---- Traits (§2.3) ----

  /** 重撃: power +6 on the card, and the next turn start recovers 1 less. */
  HeavyBlowPower: 6,

  HeavyBlowRecoveryPenalty: 1,

  /** §2.3: the trait vocabulary — 13 conditions × 10 effects (v4.4: 移動後 replaced 連動(ムーブ) and made the thirteenth). */
  TraitConditions: 13,

  TraitEffects: 10,

  /** §10: cards whose condition is a gap threshold (4 cells of the grid × 4). */
  PositionTraitCards: 16,

  /** §7.2: at most 4 of the 80 kinds may scale with N ("N 1 につき +x"). */
  GapScalingCardsMax: 4,

  /** §3.1: a card with no trait takes +2 on every column (素直な札). */
  PlainCardBonus: 2,

  /** §2.3: the same condition + effect pair appears on at most 3 kinds. */
  SameTraitMax: 3,

  /**
   * §2.3 手薄: the hand left after the play holds at most this many cards. The canon has 2
   * (§2.3, §19.2 S3) and the card and enemy tables 1; 2 was decided on 2026-09-23 (#193).
   */
  ThinHandMax: 2,

  /** §2.3 締め: the play is the third of the turn or later. */
  FinisherPlayNumber: 3,

  /** §2.3 崩し後: the opponent's stamina is below this. */
  BrokenBelow: 3,

  /** §2.3 追撃: the next attack face this turn gets this much power. */
  FollowUpPower: 5,

  // ---- Status (§5) ----

  /** §5: the player may hold six kinds at once. Enemies have no kind cap. */
  StatusKindsPlayer: 6,

  /** §3.1: applying a status gives 2 stacks unless the face says otherwise. */
  StatusApplyDefault: 2,

  /**
   * §5 (#205): a ターンで減る型 word holds at most this many stacks, on either side; whatever an
   * application would put over it is dropped. Column 4's 出血 / 再生 fits whole. The 使うと減る型
   * is not counted (§19.5 S14). Confirmed by the owner on 2026-09-26 (§23).
   */
  TurnDecayStackMax: 4,

  /** §3.1 / §5: 出血 and 再生 move HP by this much per stack at the holder's turn start. */
  BleedPerStack: 2,

  RegenPerStack: 2,

  /** §5 威圧: the holder's next action loses this much power and Guard. */
  IntimidatePenalty: 3,

  /** §5 疲労 / §9 step 2: the holder recovers this much less while it holds the word. */
  FatiguePenalty: 1,

  /** §5: the boss-only statuses. */
  BossStatusKinds: 2,

  // ---- Elites, bosses, mastery, chains (§6 / §12 / §17.8) ----

  /** §6: an elite or a boss takes two actions in its phase. */
  EliteActions: 2,

  /** §6: an elite's omen shows two steps; a normal enemy's shows one. */
  OmenDepthElite: 2,

  /** §17.8: the mastery steps (placeholder). */
  MasteryThresholds: Object.freeze([3, 8, 15]),

  /** §3.2 / §19.3 S8: a 型替え card's steps (placeholder). Its trait is hard to meet, so it starts lower. */
  MasteryThresholdsReform: Object.freeze([2, 6, 12]),

  /** §3.2 / §17.8: the cards of one life that may hold the third step. The others stop at the second. */
  MasteryTopRankCards: 1,

  /** §3.2 / §17.8 才能: the one card picked at the start gathers its ticks this many times over. */
  TalentTickMultiplier: 2,

  /** §3.2 / §17.8 習熟訓練: the ticks one training (one 刻限) gives the card it names. */
  TrainingTicks: 1,

  /** §12: battles in a chain by default (a nine-battle order may be picked too). */
  ChainBattlesDefault: 3,

  /** §3.1 / §12: the rest between chained battles (階層間の休憩) gives back this share of max HP, and all stamina. */
  ChainRestHpPercent: 30,

  // ---- Cells and gap (§7, v4.3) ----

  /**
   * §7.1: the widths a battle may be handed. The width itself is not a constant (2026-09-23,
   * #169): the layer and the battle decide it, and the dungeon side picks the values (#168).
   */
  FieldCellsMin: 5,

  FieldCellsMax: 8,

  /** §7.1 `CELL_CAPACITY`: one enemy per cell (2026-09-23). The player never shares a cell. */
  CellCapacity: 1,

  /** §7.4 `ENEMIES_MAX`: enemies on the field at once, each on its own cell. */
  EnemiesMax: 3,

  /** §7.1: the cells a large enemy may use. */
  EnemySizeMax: 3,

  /** §7.3: the player starts on cell 2, one cell of room behind. */
  PlayerStartCell: 2,

  /**
   * §7.3 `START_GAP`: the opening gap (3 since 2026-09-23, #169). The enemy's near edge starts
   * this many empty cells to the right of the player; see Field.EnemyStartCell
   * for a line too short to hold it.
   */
  StartGap: 3,

  /** §7.3: a move face, a push or a pull covers at most 2 cells at once. */
  MoveStepMax: 2,

  /** §7.3: damage per cell a pushed side could not move (叩き台). Guard reduces it. */
  WallDamage: 3,

  // ---- Resolution order (§2.2 / §17.6 F5) ----

  /**
   * The order faces resolve in within one card. The trait is evaluated once before all of
   * them, so it is not a member of this list.
   * (FaceKind.Attack, FaceKind.Move, FaceKind.Guard, FaceKind.Skill, FaceKind.Stance — see the
   * module header for why these are the member strings.)
   */
  FaceOrder: Object.freeze(['Attack', 'Move', 'Guard', 'Skill', 'Stance']),

  // ---- Damage multipliers (§5.1) ----

  /** §5.1: 強化 and 脆化 both multiply by 1.5, and one blow takes only one of them, 脆化 first (§19.5 S13). */
  EmpowerMult: 1.5,

  FragileMult: 1.5,
});
