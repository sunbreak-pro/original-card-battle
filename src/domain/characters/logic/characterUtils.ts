/**
 * Character Utility Functions
 *
 * Factory functions for character-related state objects.
 */

import type { BuffDebuffMap, BattleStats } from '@/types';

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
