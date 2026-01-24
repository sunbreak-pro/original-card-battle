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
 */
export type ElementType = "fire" | "ice" | "lightning" | "earth" | "wind" | "dark" | "light";

/**
 * Elemental State for Mage class
 *
 * Chain effects:
 * - 2 chain: Elemental damage +20%
 * - 3 chain: Element-specific debuff
 * - 4+ chain: Elemental explosion (AoE)
 */
export interface ElementalState {
  /** Discriminant for union type */
  type: "elemental";
  /** Last element used (for chain tracking) */
  lastElement: ElementType | null;
  /** Consecutive same-element card usage count */
  chainCount: number;
  /** Accumulated elemental power per element */
  chargedElements: Map<ElementType, number>;
}

/**
 * Creates initial Elemental state for Mage
 */
export function createInitialElemental(): ElementalState {
  return {
    type: "elemental",
    lastElement: null,
    chainCount: 0,
    chargedElements: new Map(),
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
