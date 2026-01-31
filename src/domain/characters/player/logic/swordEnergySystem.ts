/**
 * Swordsman Class Ability: Sword Energy System
 *
 * [Sword Energy Gauge]
 * Max: 10
 * Initial: 0
 *
 * [Sword Energy Accumulation]
 * Physical attack cards accumulate sword energy:
 * - 0 cost: +1 sword energy
 * - 1 cost: +1 sword energy
 * - 2 cost: +2 or +3 sword energy
 * - 3+ cost: +3 sword energy
 * - Dedicated sword energy cards: +4 sword energy
 *
 * [Sword Energy Effects]
 * - Physical damage = base power + (sword energy × 1)
 * - 5+: 20% chance to apply bleed on attack
 * - 8+: 40% chance to apply bleed on attack
 * - 10 (max): 60% chance to apply bleed on attack
 */

import type { Card } from '@/types/cardTypes';
import type { SwordEnergyState } from '@/types/characterTypes';
import {
  SWORD_ENERGY_MAX,
  createInitialSwordEnergy,
} from '../../logic/classAbilityUtils';
import type { ClassAbilitySystem, DamageModifier } from "../../classAbility/classAbilitySystem";
import {
  SWORD_ENERGY_BLEED_CHANCE_5,
  SWORD_ENERGY_BLEED_CHANCE_8,
  SWORD_ENERGY_BLEED_CHANCE_10,
} from '@/constants';

// Re-export for backward compatibility
export type { SwordEnergyState };
export { SWORD_ENERGY_MAX, createInitialSwordEnergy };

// ============================================================
// Sword Energy System Implementation
// ============================================================

/**
 * Sword Energy System implementing ClassAbilitySystem interface
 */
export const SwordEnergySystem: ClassAbilitySystem<SwordEnergyState> = {
  /**
   * Initialize sword energy state
   */
  initialize(): SwordEnergyState {
    return createInitialSwordEnergy();
  },

  /**
   * Handle card play - add sword energy for attack cards
   */
  onCardPlay(state: SwordEnergyState, card: Card): SwordEnergyState {
    // Only swordsman cards or physical attacks gain energy
    if (card.characterClass !== "swordsman" && card.characterClass !== "common") {
      return state;
    }

    // Handle energy consumption cards first
    if (card.swordEnergyConsume !== undefined && card.swordEnergyConsume > 0) {
      const consumed = Math.min(state.current, card.swordEnergyConsume);
      return {
        ...state,
        current: state.current - consumed,
      };
    }

    // Handle energy consumption for "consume all" cards (swordEnergyConsume = 0)
    if (card.swordEnergyConsume === 0) {
      return {
        ...state,
        current: 0, // Consume all
      };
    }

    // Handle energy gain
    const energyGain = card.swordEnergyGain ??
      calculateSwordEnergyGain(card.cost, card.category === "swordEnergy");

    if (energyGain > 0) {
      return addSwordEnergy(state, energyGain);
    }

    return state;
  },

  /**
   * Handle turn start
   */
  onTurnStart(state: SwordEnergyState): SwordEnergyState {
    // Sword energy persists between turns
    return state;
  },

  /**
   * Handle turn end
   */
  onTurnEnd(state: SwordEnergyState): SwordEnergyState {
    // Sword energy persists between turns
    return state;
  },

  /**
   * Get damage modifier based on sword energy level
   */
  getDamageModifier(state: SwordEnergyState, _card?: Card): DamageModifier {
    return {
      flatBonus: state.current, // +1 damage per sword energy
      percentMultiplier: 1.0,
      critBonus: 0,
      penetration: 0,
    };
  },

  /**
   * Check if sword energy action can be performed
   */
  canPerformAction(state: SwordEnergyState, actionId: string): boolean {
    switch (actionId) {
      case "consume_3":
        return state.current >= 3;
      case "consume_5":
        return state.current >= 5;
      case "consume_all":
        return state.current > 0;
      case "max_energy_attack":
        return state.current >= SWORD_ENERGY_MAX;
      default:
        return true;
    }
  },

  /**
   * Get description of current sword energy state
   */
  getStateDescription(state: SwordEnergyState): string {
    if (state.current >= SWORD_ENERGY_MAX) {
      return `剣気 MAX (${state.current}/${state.max}) - 出血付与60%!`;
    }
    if (state.current >= 8) {
      return `剣気 ${state.current}/${state.max} - 出血付与40%`;
    }
    if (state.current >= 5) {
      return `剣気 ${state.current}/${state.max} - 出血付与20%`;
    }
    return `剣気 ${state.current}/${state.max}`;
  },
};

// ============================================================
// Legacy Helper Functions (Backward Compatibility)
// ============================================================

/**
 * Calculate sword energy gain based on card cost
 */
export function calculateSwordEnergyGain(
  cost: number,
  isSwordEnergyCard: boolean = false,
  customGain?: number
): number {
  if (customGain !== undefined) {
    return customGain;
  }

  if (isSwordEnergyCard) {
    return 4;
  }

  if (cost === 0) return 1;
  if (cost === 1) return 1;
  if (cost === 2) return 2;
  return 3;
}

/**
 * Add sword energy to current state
 */
export function addSwordEnergy(
  state: SwordEnergyState,
  amount: number
): SwordEnergyState {
  return {
    ...state,
    current: Math.min(state.max, state.current + amount),
  };
}

/**
 * Consume a specific amount of sword energy
 */
export function consumeSwordEnergy(
  state: SwordEnergyState,
  amount: number
): { newState: SwordEnergyState; consumed: number } {
  const consumed = Math.min(state.current, amount);
  return {
    newState: {
      ...state,
      current: state.current - consumed,
    },
    consumed,
  };
}

/**
 * Consume all sword energy
 */
export function consumeAllSwordEnergy(
  state: SwordEnergyState
): { newState: SwordEnergyState; consumed: number } {
  const consumed = state.current;
  return {
    newState: {
      ...state,
      current: 0,
    },
    consumed,
  };
}

/**
 * Calculate bleed chance based on sword energy level
 */
export function calculateSwordEnergyBleedChance(swordEnergy: number): number {
  if (swordEnergy >= 10) return SWORD_ENERGY_BLEED_CHANCE_10;
  if (swordEnergy >= 8) return SWORD_ENERGY_BLEED_CHANCE_8;
  if (swordEnergy >= 5) return SWORD_ENERGY_BLEED_CHANCE_5;
  return 0;
}

/**
 * Sword energy effect summary
 */
export interface SwordEnergyEffects {
  bleedChance: number;
  isMaxEnergy: boolean;
}

/**
 * Get all sword energy effects at once
 */
export function getSwordEnergyEffects(swordEnergy: number): SwordEnergyEffects {
  return {
    bleedChance: calculateSwordEnergyBleedChance(swordEnergy),
    isMaxEnergy: swordEnergy >= SWORD_ENERGY_MAX,
  };
}

/**
 * Get bleed chance from sword energy state
 */
export function getSwordEnergyBleedChance(state: SwordEnergyState): number {
  return calculateSwordEnergyBleedChance(state.current);
}
