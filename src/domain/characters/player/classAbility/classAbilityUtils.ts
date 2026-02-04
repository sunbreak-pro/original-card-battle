/**
 * Class Ability Utility Functions
 *
 * Factory functions and type guards for class ability states.
 */

import type {
  CharacterClass,
  SwordEnergyState,
  ElementalState,
  ClassAbilityState,
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

/**
 * Creates initial class ability state based on character class
 */
export function createInitialClassAbility(characterClass: CharacterClass): ClassAbilityState {
  switch (characterClass) {
    case "swordsman":
      return createInitialSwordEnergy();
    case "mage":
      return createInitialElemental();
    default: {
      const _exhaustive: never = characterClass;
      throw new Error(`Unknown character class: ${_exhaustive}`);
    }
  }
}

/**
 * Type guard for SwordEnergyState
 */
export function isSwordEnergyState(state: ClassAbilityState): state is SwordEnergyState {
  return state.type === "swordEnergy";
}

/**
 * Type guard for ElementalState
 */
export function isElementalState(state: ClassAbilityState): state is ElementalState {
  return state.type === "elemental";
}
