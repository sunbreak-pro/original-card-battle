/**
 * Class Ability Utility Functions
 *
 * Factory functions and type guards for class ability states.
 */

import type {
  SwordEnergyState,
  ElementalState,
} from '@/types/characterTypes';

export const SWORD_ENERGY_MAX = 10;

/**
 * Creates initial Sword Energy state
 */
export function createInitialSwordEnergy(): SwordEnergyState {
  return {
    type: "swordEnergy",
    current: 0,
    max: SWORD_ENERGY_MAX,
  };
}

/**
 * Creates initial Elemental state for Mage
 */
export function createInitialElemental(): ElementalState {
  return {
    type: "elemental",
    lastElement: null,
    resonanceLevel: 0,
  };
}
