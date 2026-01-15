import type { ExplorationLimit, SanctuaryProgress } from "../../camps/types/CampTypes";
import type { MagicStones } from "../../item_equipment/type/ItemTypes";
import type { EquipmentSlots, InventoryState, StorageState, EquipmentInventoryState } from "../../camps/types/StorageTypes";
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

export interface ExtendedPlayer extends Player {
  storage: StorageState;
  inventory: InventoryState;
  equipmentInventory: EquipmentInventoryState; // Spare equipment for dungeon swaps (max 3)
  equipmentSlots: EquipmentSlots;
  explorationGold: number; // Lost on death
  baseCampGold: number;
  explorationMagicStones: MagicStones;
  baseCampMagicStones: MagicStones;
  explorationLimit: ExplorationLimit;
  sanctuaryProgress: SanctuaryProgress;
}
