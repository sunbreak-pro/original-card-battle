/**
 * Class Ability System Interface
 *
 * Defines the common interface for all character class ability systems.
 * Each class (Swordsman, Mage, Summoner) implements this interface with
 * their specific ability state type.
 */

import type { Card } from '@/types/cardTypes';
import type { ClassAbilityState } from '@/types/characterTypes';

// ============================================================
// Damage Modifier Interface
// ============================================================

/**
 * Damage modifier returned by class ability systems
 *
 * Used to modify damage calculations based on ability state.
 */
export interface DamageModifier {
  /** Flat damage bonus added to base damage */
  flatBonus: number;
  /** Percentage multiplier (1.0 = 100%, 1.5 = 150%) */
  percentMultiplier: number;
  /** Critical hit chance bonus (0.0 - 1.0) */
  critBonus: number;
  /** Penetration bonus (0.0 - 1.0, bypasses armor) */
  penetration: number;
}

/**
 * Default damage modifier with no bonuses
 */
export const DEFAULT_DAMAGE_MODIFIER: DamageModifier = {
  flatBonus: 0,
  percentMultiplier: 1.0,
  critBonus: 0,
  penetration: 0,
};
/**
 * Generic interface for class ability systems
 *
 * @template T - The specific ability state type (SwordEnergyState, ElementalState, SummonState)
 */
export interface ClassAbilitySystem<T extends ClassAbilityState> {
  /**
   * Initialize the ability state
   * Called at the start of battle
   */
  initialize(): T;

  /**
   * Handle card play event
   * Called when a card is played
   *
   * @param state - Current ability state
   * @param card - The card being played
   * @returns Updated ability state
   */
  onCardPlay(state: T, card: Card): T;

  /**
   * Handle turn start event
   * Called at the beginning of each player turn
   *
   * @param state - Current ability state
   * @returns Updated ability state
   */
  onTurnStart(state: T): T;

  /**
   * Handle turn end event
   * Called at the end of each player turn
   *
   * @param state - Current ability state
   * @returns Updated ability state
   */
  onTurnEnd(state: T): T;

  /**
   * Get damage modifiers based on current ability state
   *
   * @param state - Current ability state
   * @param card - The card being used for attack (optional)
   * @returns Damage modifier to apply
   */
  getDamageModifier(state: T, card?: Card): DamageModifier;

  /**
   * Check if a specific ability action can be performed
   *
   * @param state - Current ability state
   * @param actionId - Identifier for the action
   * @returns Whether the action can be performed
   */
  canPerformAction(state: T, actionId: string): boolean;

  /**
   * Get a description of the current ability state
   * Used for UI display
   *
   * @param state - Current ability state
   * @returns Human-readable description
   */
  getStateDescription(state: T): string;
}

// ============================================================
// Helper Functions
// ============================================================

/**
 * Combine multiple damage modifiers
 *
 * Flat bonuses are added, multipliers are multiplied,
 * crit and penetration bonuses are added (capped at 1.0)
 */
export function combineDamageModifiers(...modifiers: DamageModifier[]): DamageModifier {
  return modifiers.reduce(
    (combined, modifier) => ({
      flatBonus: combined.flatBonus + modifier.flatBonus,
      percentMultiplier: combined.percentMultiplier * modifier.percentMultiplier,
      critBonus: Math.min(1.0, combined.critBonus + modifier.critBonus),
      penetration: Math.min(1.0, combined.penetration + modifier.penetration),
    }),
    { ...DEFAULT_DAMAGE_MODIFIER }
  );
}

/**
 * Apply damage modifier to base damage
 */
export function applyDamageModifier(baseDamage: number, modifier: DamageModifier): number {
  return Math.floor((baseDamage + modifier.flatBonus) * modifier.percentMultiplier);
}
