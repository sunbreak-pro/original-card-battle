import type { ExplorationLimit, SanctuaryProgress } from "../../camps/types/CampTypes";
import type { MagicStones } from "../../camps/types/ItemTypes";
import type { EquipmentSlots, InventoryState, StorageState } from "../../camps/types/StorageTypes";
import type { Card } from "../../cards/type/cardType";
import type { Character } from "./characterType";
import type { CharacterClass } from "./characterType";

export interface Player extends Character {
  name?: string;
  playerClass: CharacterClass;
  classGrade: string;
  level: number;
  hp: number;
  maxHp: number;
  ap: number;
  maxAp: number;
  guard: number;
  speed: number;
  cardActEnergy: number;
  gold: number;
  deck: Card[];
  buffDebuffs: Character["buffDebuffs"];
  equipment?: string[];
  equipmentAtkPercent?: number;
  equipmentDefPercent?: number;
  tittle?: string[];
}
/**
 * Extended Player interface for BaseCamp system
 * Adds storage, inventory, resources, and progression tracking
 */

export interface ExtendedPlayer extends Player {
  // Storage & Inventory (from StorageTypes)
  storage: StorageState;
  inventory: InventoryState;
  equipmentSlots: EquipmentSlots;

  // Resource tracking (separated for death penalty)
  explorationGold: number; // Lost on death
  baseCampGold: number; // Kept on death
  explorationMagicStones: MagicStones; // Lost on death
  baseCampMagicStones: MagicStones; // Kept on death

  // Progression tracking
  explorationLimit: ExplorationLimit;
  sanctuaryProgress: SanctuaryProgress;
}
