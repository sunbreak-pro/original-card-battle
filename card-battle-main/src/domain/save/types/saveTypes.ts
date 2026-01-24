/**
 * Save System Type Definitions
 *
 * Defines the structure of save data for the roguelike card game.
 * Save data is stored in LocalStorage and includes all persistent state.
 */

import type { CharacterClass } from "../../characters/type/baseTypes";
import type { MagicStones, Item } from "../../item_equipment/type/ItemTypes";
import type { EquipmentSlots } from "../../camps/types/StorageTypes";
import type { SanctuaryProgress, Depth } from "../../camps/types/CampTypes";

/**
 * Save data version for migration handling
 */
export const SAVE_VERSION = "1.0.0";

/**
 * LocalStorage key for save data
 */
export const SAVE_KEY = "roguelike_card_save";

/**
 * Player save data
 * Contains core player identity and stats
 */
export interface PlayerSaveData {
  /** Player display name */
  name: string;
  /** Character class */
  playerClass: CharacterClass;
  /** Class grade (E, D, C, B, A, S, SS) */
  classGrade: string;
  /** Player level */
  level: number;
  /** Current HP */
  hp: number;
  /** Maximum HP */
  maxHp: number;
  /** Current AP (armor points) */
  ap: number;
  /** Maximum AP */
  maxAp: number;
  /** Player speed */
  speed: number;
  /** Deck card IDs */
  deckCardIds: string[];
}

/**
 * Resource save data
 * Contains gold and magic stones (only baseCamp values - exploration is lost on death)
 */
export interface ResourceSaveData {
  /** Gold stored at BaseCamp */
  baseCampGold: number;
  /** Magic stones stored at BaseCamp */
  baseCampMagicStones: MagicStones;
}

/**
 * Inventory save data
 * Contains storage items and equipment
 */
export interface InventorySaveData {
  /** Items in permanent storage */
  storageItems: Item[];
  /** Currently equipped items */
  equipmentSlots: EquipmentSlots;
}

/**
 * Progression save data
 * Contains unlocks and achievements
 */
export interface ProgressionSaveData {
  /** Sanctuary skill tree progress */
  sanctuaryProgress: SanctuaryProgress;
  /** Unlocked dungeon depths */
  unlockedDepths: Depth[];
}

/**
 * Complete save data structure
 */
export interface SaveData {
  /** Save file version for migration */
  version: string;
  /** Timestamp of save */
  timestamp: number;
  /** Player data */
  player: PlayerSaveData;
  /** Resource data */
  resources: ResourceSaveData;
  /** Inventory data */
  inventory: InventorySaveData;
  /** Progression data */
  progression: ProgressionSaveData;
}

/**
 * Save operation result
 */
export interface SaveResult {
  success: boolean;
  message: string;
  timestamp?: number;
}

/**
 * Load operation result
 */
export interface LoadResult {
  success: boolean;
  message: string;
  data?: SaveData;
  needsMigration?: boolean;
}

/**
 * Save metadata (for displaying save info without loading full data)
 */
export interface SaveMetadata {
  exists: boolean;
  version?: string;
  timestamp?: number;
  playerName?: string;
  playerClass?: CharacterClass;
  classGrade?: string;
}
