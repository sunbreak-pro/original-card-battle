/**
 * Card Execution Type Definitions
 *
 * Types for card execution results and effect previews.
 */

import type { BuffDebuffState } from "./baffType";

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

/**
 * Default card execution result
 */
export function createDefaultExecutionResult(): CardExecutionResult {
  return {
    success: false,
    damageDealt: 0,
    guardGained: 0,
    healingDone: 0,
    energyConsumed: 0,
    swordEnergyChange: 0,
    swordEnergyConsumed: 0,
    buffsApplied: [],
    debuffsApplied: [],
    cardsDrawn: 0,
    isCritical: false,
    lifestealAmount: 0,
    reflectDamage: 0,
    bleedDamage: 0,
  };
}
