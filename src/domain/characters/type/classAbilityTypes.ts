/**
 * Class Ability Type Definitions
 *
 * Defines the state types for each character class's unique ability system:
 * - Swordsman: Sword Energy (剣気)
 * - Mage: Elemental Chain (属性連鎖) - Future
 * - Summoner: Summon System (召喚) - Future
 */

import type { CharacterClass } from "./baseTypes";

// ============================================================
// Swordsman: Sword Energy System
// ============================================================

/**
 * Sword Energy State
 *
 * Max: 10
 * Initial: 0
 *
 * Effects:
 * - Physical damage = base + (swordEnergy × 2)
 * - 5+: Critical rate +20%
 * - 8+: Penetration +30%
 * - 10 (max): Guaranteed critical + 50% penetration
 */
export interface SwordEnergyState {
  /** Discriminant for union type */
  type: "swordEnergy";
  /** Current sword energy (0-10) */
  current: number;
  /** Maximum sword energy (10) */
  max: number;
}

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

// ============================================================
// Mage: Elemental Chain System (Future Implementation)
// ============================================================

/**
 * Element types for Mage class
 * Limited to 5 elements for initial implementation
 */
export type ElementType = "fire" | "ice" | "lightning" | "dark" | "light";

/**
 * Resonance level for elemental chain effects
 * - 0: No resonance (base effects)
 * - 1: First resonance (damage +15%, element-specific effect)
 * - 2: Maximum resonance (damage +30%, stronger effect + field buff)
 */
export type ResonanceLevel = 0 | 1 | 2;

/**
 * Elemental State for Mage class
 *
 * Resonance effects (same element chain):
 * - Level 0: Base damage only
 * - Level 1: Damage +15%, minor element effect
 * - Level 2: Damage +30%, major element effect + field buff
 */
export interface ElementalState {
  /** Discriminant for union type */
  type: "elemental";
  /** Last element used (for resonance tracking) */
  lastElement: ElementType | null;
  /** Current resonance level (0-2) */
  resonanceLevel: ResonanceLevel;
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

// ============================================================
// Summoner: Summon System (Future Implementation)
// ============================================================

/**
 * Summon types
 */
export type SummonType = "offensive" | "defensive" | "support";

/**
 * Summon ability definition
 */
export interface SummonAbility {
  id: string;
  name: string;
  description: string;
  cooldown: number;
  currentCooldown: number;
}

/**
 * Individual summon entity
 */
export interface Summon {
  id: string;
  name: string;
  type: SummonType;
  hp: number;
  maxHp: number;
  /** Remaining turns before despawn */
  duration: number;
  abilities: SummonAbility[];
}

/**
 * Summon State for Summoner class
 */
export interface SummonState {
  /** Discriminant for union type */
  type: "summon";
  /** Active summons (max 3) */
  activeSummons: Summon[];
  /** Number of unlocked summon slots */
  summonSlots: number;
  /** Bond level with summons (multiplier for effects) */
  bondLevel: number;
}

/**
 * Creates initial Summon state for Summoner
 */
export function createInitialSummon(): SummonState {
  return {
    type: "summon",
    activeSummons: [],
    summonSlots: 1,
    bondLevel: 1,
  };
}

// ============================================================
// Union Type and Factory
// ============================================================

/**
 * Union type for all class ability states
 */
export type ClassAbilityState = SwordEnergyState | ElementalState | SummonState;

/**
 * Creates initial class ability state based on character class
 */
export function createInitialClassAbility(characterClass: CharacterClass): ClassAbilityState {
  switch (characterClass) {
    case "swordsman":
      return createInitialSwordEnergy();
    case "mage":
      return createInitialElemental();
    case "summoner":
      return createInitialSummon();
    default: {
      // Exhaustive check
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

/**
 * Type guard for SummonState
 */
export function isSummonState(state: ClassAbilityState): state is SummonState {
  return state.type === "summon";
}
