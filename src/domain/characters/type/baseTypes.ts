/**
 * Base Types for Character System
 *
 * This file contains fundamental types shared across all character types:
 * - BattleStats: Common combat statistics for both Player and Enemy
 * - CharacterClass: Player class enumeration
 */

import type { BuffDebuffMap } from "../../battles/type/baffType";

/**
 * Character class types
 * - swordsman: Physical combat specialist with Sword Energy system
 * - mage: Magic specialist with Elemental Chain system (future)
 * - summoner: Summoning specialist with Summon system (future)
 */
export type CharacterClass = "swordsman" | "mage" | "summoner";

/**
 * Extended character class including common cards
 * Used for Card type to indicate which class can use the card
 */
export type CardCharacterClass = CharacterClass | "common";

/**
 * Battle Statistics Interface
 *
 * Common combat stats shared between Player and Enemy during battle.
 * This is the base for all battle-time state management.
 */
export interface BattleStats {
  /** Current hit points */
  hp: number;
  /** Maximum hit points */
  maxHp: number;
  /** Current armor points (damage reduction) */
  ap: number;
  /** Maximum armor points */
  maxAp: number;
  /** Current guard value (temporary shield) */
  guard: number;
  /** Speed determines action order */
  speed: number;
  /** Active buff/debuff effects */
  buffDebuffs: BuffDebuffMap;
}

/**
 * Creates a default empty BuffDebuffMap
 */
export function createEmptyBuffDebuffMap(): BuffDebuffMap {
  return new Map();
}

/**
 * Creates initial BattleStats with specified values
 */
export function createBattleStats(
  maxHp: number,
  maxAp: number,
  speed: number,
  startingGuard: number = 0
): BattleStats {
  return {
    hp: maxHp,
    maxHp,
    ap: maxAp,
    maxAp,
    guard: startingGuard,
    speed,
    buffDebuffs: createEmptyBuffDebuffMap(),
  };
}
