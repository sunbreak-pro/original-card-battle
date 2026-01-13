// Death Handler - Manages player death penalty system
// Clears inventory/equipment but retains storage and base camp resources

import type { Player } from "../../characters/type/playerTypes";

/**
 * Handle Player Death
 *
 * Death Penalty:
 * - LOST: All inventory items, all equipped items, exploration resources
 * - KEPT: Storage items, base camp resources, permanent progression
 *
 * @param player - Current player state
 * @returns Updated player state after death penalty
 */
export function handlePlayerDeath(player: Player): Player {
  return {
    ...player,

    // Clear all inventory items
    inventory: {
      ...player.inventory,
      items: [],
      currentCapacity: 0,
    },

    // Clear all equipment slots
    equipmentSlots: {
      weapon: null,
      armor: null,
      helmet: null,
      boots: null,
      accessory1: null,
      accessory2: null,
    },

    // Reset exploration resources (lost on death)
    explorationGold: 0,
    explorationMagicStones: {
      small: 0,
      medium: 0,
      large: 0,
    },

    // Reset current run souls (lost on death)
    sanctuaryProgress: {
      ...player.sanctuaryProgress,
      currentRunSouls: 0,
      // totalSouls is RETAINED (permanent progression)
    },

    // Storage is STRICTLY UNTOUCHED - this is the safety net
    // storage: { ...player.storage } (no change needed, kept as-is)

    // Base camp resources are RETAINED
    // baseCampGold: stays the same
    // baseCampMagicStones: stays the same

    // Increment death counter
    explorationLimit: {
      ...player.explorationLimit,
      current: player.explorationLimit.current + 1,
    },

    // Return to camp with minimal HP, no AP
    hp: 1,
    ap: 0,
  };
}

/**
 * Check if player is dead (hp <= 0)
 *
 * @param player - Current player state
 * @returns true if player is dead, false otherwise
 */
export function isPlayerDead(player: Player): boolean {
  return player.hp <= 0;
}
