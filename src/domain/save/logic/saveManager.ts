/**
 * Save Manager
 *
 * Handles saving and loading game data to/from LocalStorage.
 * Provides version checking and migration support.
 */

import type {
  SaveData,
  SaveResult,
  LoadResult,
  SaveMetadata,
} from '@/types/saveTypes';
import { SAVE_KEY, SAVE_VERSION } from '@/constants/saveConstants';
import { logger } from '@/utils/logger';

/**
 * Save Manager object with all save/load operations
 */
export const saveManager = {
  /**
   * Save game data to LocalStorage
   * @param data - Complete save data to store
   * @returns SaveResult indicating success or failure
   */
  save(data: Omit<SaveData, "version" | "timestamp">): SaveResult {
    try {
      const saveData: SaveData = {
        ...data,
        version: SAVE_VERSION,
        timestamp: Date.now(),
      };

      const jsonString = JSON.stringify(saveData);
      localStorage.setItem(SAVE_KEY, jsonString);

      return {
        success: true,
        message: "Game saved successfully",
        timestamp: saveData.timestamp,
      };
    } catch (error) {
      logger.error("Save failed:", error);
      return {
        success: false,
        message: `Save failed: ${error instanceof Error ? error.message : "Unknown error"}`,
      };
    }
  },

  /**
   * Load game data from LocalStorage
   * @returns LoadResult with data if successful
   */
  load(): LoadResult {
    try {
      const jsonString = localStorage.getItem(SAVE_KEY);

      if (!jsonString) {
        return {
          success: false,
          message: "No save data found",
        };
      }

      const data = JSON.parse(jsonString) as SaveData;

      // Version check — auto-migrate if needed
      if (data.version !== SAVE_VERSION) {
        const migrated = this.migrate(data);
        // Persist the migrated data so future loads don't re-migrate
        localStorage.setItem(SAVE_KEY, JSON.stringify(migrated));
        return {
          success: true,
          message: `Save data migrated from ${data.version} to ${SAVE_VERSION}.`,
          data: migrated,
          needsMigration: false,
        };
      }

      return {
        success: true,
        message: "Game loaded successfully",
        data,
      };
    } catch (error) {
      logger.error("Load failed:", error);
      return {
        success: false,
        message: `Load failed: ${error instanceof Error ? error.message : "Unknown error"}`,
      };
    }
  },

  /**
   * Delete save data from LocalStorage
   */
  deleteSave(): void {
    localStorage.removeItem(SAVE_KEY);
  },

  /**
   * Check if save data exists
   * @returns true if save data exists
   */
  hasSave(): boolean {
    return localStorage.getItem(SAVE_KEY) !== null;
  },

  /**
   * Get save metadata without loading full data
   * @returns SaveMetadata with basic save info
   */
  getMetadata(): SaveMetadata {
    try {
      const jsonString = localStorage.getItem(SAVE_KEY);

      if (!jsonString) {
        return { exists: false };
      }

      const data = JSON.parse(jsonString) as SaveData;

      return {
        exists: true,
        version: data.version,
        timestamp: data.timestamp,
        playerName: data.player?.name,
        playerClass: data.player?.playerClass,
        classGrade: data.player?.classGrade,
      };
    } catch {
      return { exists: false };
    }
  },

  /**
   * Export save data as JSON string (for backup)
   * @returns JSON string of save data or empty string if no save exists
   */
  exportSave(): string {
    const jsonString = localStorage.getItem(SAVE_KEY);
    return jsonString || "";
  },

  /**
   * Import save data from JSON string
   * @param jsonString - JSON string to import
   * @returns true if import successful
   */
  importSave(jsonString: string): boolean {
    try {
      // Validate JSON structure
      const data = JSON.parse(jsonString) as SaveData;

      // Basic validation
      if (!data.version || !data.player || !data.resources) {
        logger.error("Invalid save data structure");
        return false;
      }

      // Store in LocalStorage
      localStorage.setItem(SAVE_KEY, jsonString);
      return true;
    } catch (error) {
      logger.error("Import failed:", error);
      return false;
    }
  },

  /**
   * Migrate save data from older versions
   * @param data - Save data to migrate
   * @returns Migrated save data
   */
  migrate(data: SaveData): SaveData {
    let migrated = { ...data };

    // Migrate from 1.0.0 to 1.1.0: add missing inventory and exploration resource fields
    if (migrated.version === "1.0.0") {
      const oldResources = migrated.resources as unknown as Record<string, unknown>;
      const oldInventory = migrated.inventory as unknown as Record<string, unknown>;

      migrated = {
        ...migrated,
        resources: {
          ...migrated.resources,
          explorationGold: (oldResources.explorationGold as number) ?? 0,
          explorationMagicStones: (oldResources.explorationMagicStones as typeof migrated.resources.explorationMagicStones) ?? { small: 0, medium: 0, large: 0, huge: 0 },
          explorationLimit: (oldResources.explorationLimit as typeof migrated.resources.explorationLimit) ?? { current: 0, max: 10 },
        },
        inventory: {
          ...migrated.inventory,
          inventoryItems: (oldInventory.inventoryItems as typeof migrated.inventory.inventoryItems) ?? [],
          equipmentInventoryItems: (oldInventory.equipmentInventoryItems as typeof migrated.inventory.equipmentInventoryItems) ?? [],
        },
        version: "1.1.0",
      };
    }

    // Ensure shopRotationDay exists (added in 1.2.0)
    if (migrated.progression && migrated.progression.shopRotationDay == null) {
      migrated = {
        ...migrated,
        progression: {
          ...migrated.progression,
          shopRotationDay: Math.floor(Date.now() / 86400000),
        },
      };
    }

    return {
      ...migrated,
      version: SAVE_VERSION,
    };
  },
};

/**
 * Create default save data structure
 * Used when starting a new game
 */
export function createDefaultSaveData(): Omit<SaveData, "version" | "timestamp"> {
  return {
    player: {
      name: "Adventurer",
      playerClass: "swordsman",
      classGrade: "E",
      level: 1,
      hp: 100,
      maxHp: 100,
      ap: 20,
      maxAp: 20,
      speed: 40,
      deckCardIds: [],
    },
    resources: {
      baseCampGold: 0,
      baseCampMagicStones: { small: 0, medium: 0, large: 0, huge: 0 },
      explorationGold: 0,
      explorationMagicStones: { small: 0, medium: 0, large: 0, huge: 0 },
      explorationLimit: { current: 0, max: 10 },
    },
    inventory: {
      storageItems: [],
      equipmentSlots: {
        weapon: null,
        armor: null,
        helmet: null,
        boots: null,
        accessory1: null,
        accessory2: null,
      },
      inventoryItems: [],
      equipmentInventoryItems: [],
    },
    progression: {
      sanctuaryProgress: {
        currentRunSouls: 0,
        totalSouls: 0,
        unlockedNodes: [],
        explorationLimitBonus: 0,
      },
      unlockedDepths: [1],
    },
  };
}

/**
 * Format timestamp for display
 * @param timestamp - Unix timestamp
 * @returns Formatted date string
 */
export function formatSaveTimestamp(timestamp: number): string {
  const date = new Date(timestamp);
  return date.toLocaleString("ja-JP", {
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
  });
}
