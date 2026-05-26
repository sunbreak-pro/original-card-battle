/**
 * Battle Type Definitions
 *
 * Centralized battle-related types including:
 * - Buff/debuff types and state
 * - Damage calculation types
 * - Phase types
 * - Card execution types
 */

// ============================================================
// Buff/Debuff Types
// ============================================================

/**
 * Represents who applied a buff/debuff.
 * Used to determine when duration should decrease.
 */
export type BuffOwner = 'player' | 'enemy' | 'environment';

export type BuffDebuffType =
  | "bleed"
  | "poison"
  | "burn"
  | "curse"
  | "overCurse"
  | "stagger"
  | "stun"
  | "freeze"
  | "atkDownMinor"
  | "atkDownMajor"
  | "defDownMinor"
  | "defDownMajor"
  | "weakness"
  | "prostoration"
  | "slow"
  | "stall"
  | "atkUpMinor"
  | "atkUpMajor"
  | "defUpMinor"
  | "defUpMajor"
  | "penetrationUp"
  | "hitRateUp"
  | "criticalUp"
  | "haste"
  | "superFast"
  | "regeneration"
  | "shieldRegen"
  | "reflect"
  | "immunity"
  | "energyRegen"
  | "drawPower"
  | "costReduction"
  | "lifesteal"
  | "doubleStrike"
  | "swordEnergyGain"
  | "elementalMastery"
  | "fireField"
  | "electroField"
  | "iceField"
  | "darkField"
  | "lightField"
  | "focus"
  | "momentum"
  | "cleanse"
  | "tenacity"
  | "lastStand";

export interface BuffEffectDefinition {
  name: string;
  nameJa: string;
  value: number;
  isDebuff: boolean;
  isPercentage: boolean;
  stackable: boolean;
  description(): string;
}

export interface CardBuffSpec {
  name: BuffDebuffType;
  duration: number;
  stacks: number;
  isPermanent?: boolean;
}

export interface BuffDebuffState {
  name: BuffDebuffType;
  stacks: number;
  duration: number;
  value: number;
  isPermanent: boolean;
  source?: string;
  /**
   * Who applied this buff/debuff. Duration decreases only at the applier's phase start.
   * Optional for backward compatibility - defaults to 'environment' when not specified.
   */
  appliedBy?: BuffOwner;
}

export type BuffDebuffMap = Map<BuffDebuffType, BuffDebuffState>;

// ============================================================
// Damage Types
// ============================================================

/** Classification of damage for defense calculation routing */
export type DamageType = "physical" | "magical" | "true";

export interface DamageResult {
  finalDamage: number;
  damageType: DamageType;
  isCritical: boolean;
  reflectDamage: number;
  lifestealAmount: number;
}

export interface DamageAllocation {
  guardDamage: number;
  apDamage: number;
  hpDamage: number;
}

/**
 * Process player card damage against enemy
 */
export interface PlayerCardDamageResult {
  totalDamage: number;
  guardDamage: number;
  apDamage: number;
  hpDamage: number;
  isCritical: boolean;
  lifestealAmount: number;
  reflectDamage: number;
}

// ============================================================
// Phase Types
// ============================================================

export type PhaseActor = "player" | "enemy";

export interface PhaseEntry {
  actor: PhaseActor;
  /** Index into enemies array, only relevant when actor === "enemy" */
  enemyIndex?: number;
}

export interface PhaseQueue {
  phases: PhaseActor[];
  /** Extended phase entries with per-enemy index info */
  entries: PhaseEntry[];
  currentIndex: number;
}

export interface SpeedRandomState {
  varianceHistory: number[];
}

export interface PhaseCalculationResult {
  queue: PhaseQueue;
  playerSpeed: number;
  enemySpeed: number;
  speedDifference: number;
  consecutivePhases: number;
  fasterActor: PhaseActor;
}

// ============================================================
// Card Execution Types
// ============================================================

/**
 * Result of executing a card
 */
export interface CardExecutionResult {
  /** Whether the card was successfully played */
  success: boolean;
  /** Total damage dealt to enemy */
  damageDealt: number;
  /** Guard gained by player */
  guardGained: number;
  /** HP healed */
  healingDone: number;
  /** Energy consumed for the card */
  energyConsumed: number;
  /** Change in sword energy (positive = gained, negative = consumed) */
  swordEnergyChange: number;
  /** Sword energy consumed for multiplier damage */
  swordEnergyConsumed: number;
  /** Buffs applied to player */
  buffsApplied: BuffDebuffState[];
  /** Debuffs applied to enemy */
  debuffsApplied: BuffDebuffState[];
  /** Number of cards drawn */
  cardsDrawn: number;
  /** Whether the attack was critical */
  isCritical: boolean;
  /** Lifesteal amount healed */
  lifestealAmount: number;
  /** Reflect damage taken */
  reflectDamage: number;
  /** Bleed damage taken after card play */
  bleedDamage: number;
}

/**
 * Preview of card effects before execution
 */
export interface CardEffectPreview {
  /** Estimated damage to enemy (before defense) */
  estimatedDamage: number;
  /** Guard that will be gained */
  guardGain: number;
  /** HP that will be healed */
  healAmount: number;
  /** Energy cost */
  energyCost: number;
  /** Whether the card can be played */
  canPlay: boolean;
  /** Reason why card cannot be played (if applicable) */
  cannotPlayReason?: string;
  /** Sword energy that will be gained */
  swordEnergyGain: number;
  /** Sword energy that will be consumed */
  swordEnergyConsume: number;
  /** Cards that will be drawn */
  cardDraw: number;
  /** Buffs that will be applied to player */
  playerBuffs: string[];
  /** Debuffs that will be applied to enemy */
  enemyDebuffs: string[];
}

/**
 * Context required for card execution
 */
export interface CardExecutionContext {
  /** Current player HP */
  playerHp: number;
  /** Player max HP */
  playerMaxHp: number;
  /** Current player guard */
  playerGuard: number;
  /** Current player energy */
  playerEnergy: number;
  /** Whether it's player's phase */
  isPlayerPhase: boolean;
}
