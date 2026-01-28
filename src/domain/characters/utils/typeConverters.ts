/**
 * Type Converter Utilities
 *
 * Provides functions to convert between legacy types and new architecture types.
 * Used during the migration period to maintain backward compatibility.
 */

import type { Card } from "../../cards/type/cardType";
import type { DeckState } from "../../cards/decks/deckReducter";
import type { EquipmentSlots } from "../../camps/types/StorageTypes";
import type React from "react";

import type {
  Player,
  ExtendedPlayer,
  PlayerPersistentData,
  PlayerData,
  PlayerBattleState,
} from "../type/playerTypes";

import {
  createEmptyMagicStones,
  createDefaultExplorationLimit,
  createDefaultSanctuaryProgress,
} from "../type/playerTypes";

import { createEmptyBuffDebuffMap } from "../type/baseTypes";
import { createInitialClassAbility } from "../type/classAbilityTypes";
import type { CharacterClass } from "../type/baseTypes";
import { STORAGE_MAX_CAPACITY, INVENTORY_MAX_CAPACITY, EQUIPMENT_INVENTORY_MAX, GUARD_INIT_MULTIPLIER } from "../../../constants";

import type {
  EnemyDefinition,
  EnemyBattleState,
} from "../type/enemyType";
import { generateEnemyInstanceId } from "../type/enemyType";

// ============================================================
// Player Type Converters
// ============================================================

/**
 * Create PlayerBattleState from PlayerData
 *
 * Used when starting a battle to create the runtime battle state
 * from the persistent player data.
 */
export function createPlayerBattleState(
  playerData: PlayerData,
  initialDeck: DeckState
): PlayerBattleState {
  const { persistent, inventory } = playerData;

  // Calculate equipment bonuses (placeholder - actual calculation depends on equipment system)
  const equipmentAtkPercent = calculateEquipmentAtkBonus(inventory.equipmentSlots);
  const equipmentDefPercent = calculateEquipmentDefBonus(inventory.equipmentSlots);

  return {
    // BattleStats base
    hp: persistent.baseMaxHp,
    maxHp: persistent.baseMaxHp,
    ap: persistent.baseMaxAp,
    maxAp: persistent.baseMaxAp,
    guard: 0,
    speed: persistent.baseSpeed,
    buffDebuffs: createEmptyBuffDebuffMap(),

    // Player-specific battle state
    cardEnergy: 3, // Default starting energy
    maxCardEnergy: 3,
    classAbility: createInitialClassAbility(persistent.playerClass),
    currentDeck: initialDeck,

    // Equipment bonuses
    equipmentAtkPercent,
    equipmentDefPercent,
  };
}

/**
 * Create PlayerBattleState from legacy Player type
 *
 * Used for backward compatibility during migration.
 */
export function createPlayerBattleStateFromLegacy(
  player: Player,
  initialDeck: DeckState
): PlayerBattleState {
  return {
    // BattleStats base
    hp: player.hp,
    maxHp: player.maxHp,
    ap: player.ap,
    maxAp: player.maxAp,
    guard: player.guard,
    speed: player.speed,
    buffDebuffs: player.buffDebuffs ?? createEmptyBuffDebuffMap(),

    // Player-specific battle state
    cardEnergy: player.cardActEnergy,
    maxCardEnergy: 3,
    classAbility: createInitialClassAbility(player.playerClass),
    currentDeck: initialDeck,

    // Equipment bonuses
    equipmentAtkPercent: player.equipmentAtkPercent ?? 0,
    equipmentDefPercent: player.equipmentDefPercent ?? 0,
  };
}

/**
 * Extract PlayerPersistentData from legacy Player type
 *
 * Used for migration from old save data.
 */
export function extractPersistentData(player: Player): PlayerPersistentData {
  return {
    id: generatePlayerId(),
    name: player.name ?? "Adventurer",
    playerClass: player.playerClass,
    classGrade: player.classGrade,
    level: player.level,
    baseMaxHp: player.maxHp,
    baseMaxAp: player.maxAp,
    baseSpeed: player.speed,
    cardActEnergy: player.cardActEnergy,
    deckCardIds: player.deck.map(card => card.id),
    titles: player.tittle ?? [],
  };
}

/**
 * Convert legacy ExtendedPlayer to new PlayerData
 *
 * Full conversion for migration purposes.
 */
export function convertExtendedPlayerToPlayerData(extendedPlayer: ExtendedPlayer): PlayerData {
  return {
    persistent: extractPersistentData(extendedPlayer),
    resources: {
      baseCampGold: extendedPlayer.baseCampGold,
      explorationGold: extendedPlayer.explorationGold,
      baseCampMagicStones: extendedPlayer.baseCampMagicStones,
      explorationMagicStones: extendedPlayer.explorationMagicStones,
      explorationLimit: extendedPlayer.explorationLimit,
    },
    inventory: {
      storage: extendedPlayer.storage,
      inventory: extendedPlayer.inventory,
      equipmentInventory: extendedPlayer.equipmentInventory,
      equipmentSlots: extendedPlayer.equipmentSlots,
    },
    progression: {
      sanctuaryProgress: extendedPlayer.sanctuaryProgress,
      unlockedDepths: [1], // Default: only depth 1 unlocked
      completedAchievements: [],
    },
  };
}

/**
 * Convert PlayerData back to legacy ExtendedPlayer
 *
 * Used for backward compatibility with existing UI components.
 */
export function convertPlayerDataToExtendedPlayer(
  playerData: PlayerData,
  deck: Card[]
): ExtendedPlayer {
  const { persistent, resources, inventory, progression } = playerData;

  return {
    // Character base
    hp: persistent.baseMaxHp,
    maxHp: persistent.baseMaxHp,
    ap: persistent.baseMaxAp,
    maxAp: persistent.baseMaxAp,
    guard: 0,
    speed: persistent.baseSpeed,
    buffDebuffs: createEmptyBuffDebuffMap(),

    // Player properties
    name: persistent.name,
    playerClass: persistent.playerClass,
    classGrade: persistent.classGrade,
    level: persistent.level,
    cardActEnergy: 3,
    gold: resources.baseCampGold + resources.explorationGold,
    deck,
    equipment: [],
    equipmentAtkPercent: calculateEquipmentAtkBonus(inventory.equipmentSlots),
    equipmentDefPercent: calculateEquipmentDefBonus(inventory.equipmentSlots),
    tittle: persistent.titles,

    // ExtendedPlayer properties
    storage: inventory.storage,
    inventory: inventory.inventory,
    equipmentInventory: inventory.equipmentInventory,
    equipmentSlots: inventory.equipmentSlots,
    explorationGold: resources.explorationGold,
    baseCampGold: resources.baseCampGold,
    explorationMagicStones: resources.explorationMagicStones,
    baseCampMagicStones: resources.baseCampMagicStones,
    explorationLimit: resources.explorationLimit,
    sanctuaryProgress: progression.sanctuaryProgress,
  };
}

// ============================================================
// Factory Functions
// ============================================================

/**
 * Create a new PlayerData with default values
 */
export function createDefaultPlayerData(
  name: string,
  playerClass: CharacterClass,
  initialDeck: Card[]
): PlayerData {
  const baseStats = getBaseStatsForClass(playerClass);

  return {
    persistent: {
      id: generatePlayerId(),
      name,
      playerClass,
      classGrade: "E",
      level: 1,
      baseMaxHp: baseStats.maxHp,
      baseMaxAp: baseStats.maxAp,
      baseSpeed: baseStats.speed,
      cardActEnergy: baseStats.cardActEnergy,
      deckCardIds: initialDeck.map(card => card.id),
      titles: [],
    },
    resources: {
      baseCampGold: 0,
      explorationGold: 0,
      baseCampMagicStones: createEmptyMagicStones(),
      explorationMagicStones: createEmptyMagicStones(),
      explorationLimit: createDefaultExplorationLimit(),
    },
    inventory: {
      storage: { items: [], maxCapacity: STORAGE_MAX_CAPACITY, currentCapacity: 0 },
      inventory: { items: [], maxCapacity: INVENTORY_MAX_CAPACITY, currentCapacity: 0 },
      equipmentInventory: { items: [], maxCapacity: EQUIPMENT_INVENTORY_MAX, currentCapacity: 0 },
      equipmentSlots: {
        weapon: null,
        armor: null,
        helmet: null,
        boots: null,
        accessory1: null,
        accessory2: null,
      },
    },
    progression: {
      sanctuaryProgress: createDefaultSanctuaryProgress(),
      unlockedDepths: [1],
      completedAchievements: [],
    },
  };
}

// ============================================================
// Helper Functions
// ============================================================

/**
 * Generate unique player ID
 */
function generatePlayerId(): string {
  return `player_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
}

/**
 * Get base stats for a character class
 */
function getBaseStatsForClass(playerClass: CharacterClass): {
  maxHp: number;
  maxAp: number;
  speed: number;
  cardActEnergy: number;
} {
  switch (playerClass) {
    case "swordsman":
      return { maxHp: 75, maxAp: 30, speed: 12, cardActEnergy: 3 };
    case "mage":
      return { maxHp: 60, maxAp: 20, speed: 10, cardActEnergy: 3 };
    case "summoner":
      return { maxHp: 65, maxAp: 25, speed: 11, cardActEnergy: 3 };
    default: {
      const _exhaustive: never = playerClass;
      throw new Error(`Unknown player class: ${_exhaustive}`);
    }
  }
}

/**
 * Calculate attack bonus from equipped items
 */
function calculateEquipmentAtkBonus(equipmentSlots: EquipmentSlots): number {
  let bonus = 0;

  // Iterate through all equipment slots and sum attack bonuses
  const slots = Object.values(equipmentSlots);
  for (const item of slots) {
    if (item?.effects) {
      for (const effect of item.effects) {
        if (effect.type === "atkPercent") {
          bonus += effect.value;
        }
      }
    }
  }

  return bonus;
}

/**
 * Calculate defense bonus from equipped items
 */
function calculateEquipmentDefBonus(equipmentSlots: EquipmentSlots): number {
  let bonus = 0;

  // Iterate through all equipment slots and sum defense bonuses
  const slots = Object.values(equipmentSlots);
  for (const item of slots) {
    if (item?.effects) {
      for (const effect of item.effects) {
        if (effect.type === "defPercent") {
          bonus += effect.value;
        }
      }
    }
  }

  return bonus;
}

// ============================================================
// Enemy Type Converters
// ============================================================

/**
 * Create EnemyBattleState from EnemyDefinition
 *
 * Used when starting a battle to create the runtime battle state
 * from the enemy definition.
 */
export function createEnemyBattleState(
  definition: EnemyDefinition,
  ref: React.RefObject<HTMLDivElement | null>
): EnemyBattleState {
  const buffDebuffs = createEmptyBuffDebuffMap();
  const guard = definition.startingGuard ? Math.floor(definition.baseMaxAp * GUARD_INIT_MULTIPLIER) : 0;

  return {
    // Instance identification
    instanceId: generateEnemyInstanceId(definition.id),
    definitionId: definition.id,
    definition,

    // BattleStats base
    hp: definition.baseMaxHp,
    maxHp: definition.baseMaxHp,
    ap: definition.baseMaxAp,
    maxAp: definition.baseMaxAp,
    guard,
    speed: definition.baseSpeed,
    buffDebuffs,

    // Enemy-specific battle state
    energy: definition.actEnergy,
    phaseCount: 0,
    turnCount: 0,

    // DOM reference
    ref,
  };
}

