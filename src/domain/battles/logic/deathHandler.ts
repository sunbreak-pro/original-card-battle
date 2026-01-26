// Death Handler - Manages player death penalty system
// With new Lives System: On death, lives decrease by 1 and souls are transferred 100%

import type { PlayerData } from "../../characters/type/playerTypes";

/**
 * Death result with additional info for UI
 */
export interface DeathResult {
  /** Updates to apply to PlayerData */
  updates: Partial<PlayerData>;
  /** Souls transferred to permanent pool (100% of current run souls) */
  soulsTransferred: number;
}

/**
 * Handle Player Death - Returns partial updates to apply to PlayerData
 *
 * NEW Lives System Behavior:
 * - LOST: All inventory items, all equipment inventory items, all equipped items, exploration resources
 * - KEPT: Storage items, base camp resources
 * - SOULS: 100% of current run souls are transferred to totalSouls (残滓システム)
 *
 * Note: Lives are managed separately in RuntimeBattleState (not in PlayerData)
 *
 * @param playerData - Current player data
 * @returns DeathResult with updates and transferred souls count
 */
export function handlePlayerDeath(playerData: PlayerData): Partial<PlayerData> {
  const result = handlePlayerDeathWithDetails(playerData);
  return result.updates;
}

/**
 * Handle Player Death with detailed result
 * Use this when you need to display the souls transferred amount
 */
export function handlePlayerDeathWithDetails(playerData: PlayerData): DeathResult {
  // Transfer 100% of current run souls to total (残滓システム)
  const soulsTransferred = playerData.progression.sanctuaryProgress.currentRunSouls;

  return {
    updates: {
      // Clear all inventory items (except storage which is RETAINED)
      inventory: {
        ...playerData.inventory,
        // Keep storage as-is (safety net)
        storage: playerData.inventory.storage,
        // Clear inventory items
        inventory: {
          ...playerData.inventory.inventory,
          items: [],
          currentCapacity: 0,
        },
        // Clear equipment inventory items
        equipmentInventory: {
          ...playerData.inventory.equipmentInventory,
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
      },

      // Reset exploration resources (lost on death), keep base camp resources
      resources: {
        ...playerData.resources,
        explorationGold: 0,
        explorationMagicStones: {
          small: 0,
          medium: 0,
          large: 0,
          huge: 0,
        },
        // Note: explorationLimit is deprecated - use Lives system instead
        explorationLimit: playerData.resources.explorationLimit,
      },

      // Transfer souls: 100% of current run souls go to totalSouls
      progression: {
        ...playerData.progression,
        sanctuaryProgress: {
          ...playerData.progression.sanctuaryProgress,
          // Transfer all current run souls to total
          totalSouls: playerData.progression.sanctuaryProgress.totalSouls + soulsTransferred,
          // Reset current run souls to 0
          currentRunSouls: 0,
        },
      },
    },
    soulsTransferred,
  };
}
