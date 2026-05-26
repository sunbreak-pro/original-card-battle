/**
 * Save System Type Definitions
 *
 * Defines the structure of save data for the roguelike card game.
 */

import type { CharacterClass } from './characterTypes';
import type { MagicStones, Item } from './itemTypes';
import type { EquipmentSlots, ExplorationLimit } from './campTypes';
import type { SanctuaryProgress, Depth, ShopStockState } from './campTypes';

// ============================================================
// Save Data Types
// ============================================================

export interface PlayerSaveData {
  name: string;
  playerClass: CharacterClass;
  classGrade: string;
  level: number;
  hp: number;
  maxHp: number;
  ap: number;
  maxAp: number;
  speed: number;
  deckCardIds: string[];
}

export interface ResourceSaveData {
  baseCampGold: number;
  baseCampMagicStones: MagicStones;
  explorationGold: number;
  explorationMagicStones: MagicStones;
  explorationLimit: ExplorationLimit;
}

export interface InventorySaveData {
  storageItems: Item[];
  equipmentSlots: EquipmentSlots;
  inventoryItems: Item[];
  equipmentInventoryItems: Item[];
}

export interface ProgressionSaveData {
  sanctuaryProgress: SanctuaryProgress;
  unlockedDepths: Depth[];
  shopRotationDay?: number;
  shopStockState?: ShopStockState;
}

export interface SaveData {
  version: string;
  timestamp: number;
  player: PlayerSaveData;
  resources: ResourceSaveData;
  inventory: InventorySaveData;
  progression: ProgressionSaveData;
}

export interface SaveResult {
  success: boolean;
  message: string;
  timestamp?: number;
}

export interface LoadResult {
  success: boolean;
  message: string;
  data?: SaveData;
  needsMigration?: boolean;
}

export interface SaveMetadata {
  exists: boolean;
  version?: string;
  timestamp?: number;
  playerName?: string;
  playerClass?: CharacterClass;
  classGrade?: string;
}
