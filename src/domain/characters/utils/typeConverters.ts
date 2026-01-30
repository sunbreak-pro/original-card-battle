/**
 * Type Converter Utilities
 *
 * Provides functions to create battle state and player data objects.
 */

import type { Card } from '@/types/cardTypes';
import type { DeckState } from "../../cards/decks/deckReducter";
import type { EquipmentSlots } from '@/types/campTypes';
import type React from "react";

import type {
  PlayerData,
  PlayerBattleState,
  CharacterClass,
  EnemyDefinition,
  EnemyBattleState,
} from '@/types/characterTypes';

import {
  createEmptyMagicStones,
  createDefaultExplorationLimit,
  createDefaultSanctuaryProgress,
} from '../logic/playerUtils';

import { createEmptyBuffDebuffMap } from '../logic/characterUtils';
import { createInitialClassAbility } from '../logic/classAbilityUtils';
import { STORAGE_MAX_CAPACITY, INVENTORY_MAX_CAPACITY, EQUIPMENT_INVENTORY_MAX, GUARD_INIT_MULTIPLIER } from "../../../constants";

import { generateEnemyInstanceId } from '../logic/enemyUtils';

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

  // Calculate equipment bonuses
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
