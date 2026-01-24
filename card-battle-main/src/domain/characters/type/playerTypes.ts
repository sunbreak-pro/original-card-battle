/**
 * Player Type Definitions
 *
 * This file contains all player-related type definitions.
 * The new architecture separates:
 * - Persistent data (saved to storage)
 * - Battle state (runtime only)
 * - Resources (gold, magic stones)
 * - Inventory (items, equipment)
 * - Progression (unlocks, achievements)
 */

import type { ExplorationLimit, SanctuaryProgress, Depth } from "../../camps/types/CampTypes";
import type { MagicStones } from "../../item_equipment/type/ItemTypes";
import type { EquipmentSlots, InventoryState, StorageState, EquipmentInventoryState } from "../../camps/types/StorageTypes";
import type { Card } from "../../cards/type/cardType";
import type { CharacterClass } from "./baseTypes";
import type { BattleStats } from "./baseTypes";
import type { ClassAbilityState } from "./classAbilityTypes";
import type { DeckState } from "../../cards/decks/deckReducter";
import type { BuffDebuffMap } from "../../battles/type/baffType";

// Re-export CharacterClass for convenience
export type { CharacterClass } from "./baseTypes";

// ============================================================
// LIVES SYSTEM (New Feature)
// ============================================================

/**
 * Game difficulty affecting lives and other mechanics
 */
export type Difficulty = 'easy' | 'normal' | 'hard';

/**
 * Lives system configuration
 * Replaces the old exploration limit system
 */
export interface LivesSystem {
  /** Maximum lives (depends on difficulty) */
  maxLives: number;
  /** Current remaining lives */
  currentLives: number;
}

/**
 * Maximum lives by difficulty level
 */
export const LIVES_BY_DIFFICULTY: Record<Difficulty, number> = {
  easy: 3,
  normal: 3,
  hard: 2,
};

/**
 * Create initial lives system based on difficulty
 */
export function createLivesSystem(difficulty: Difficulty): LivesSystem {
  const maxLives = LIVES_BY_DIFFICULTY[difficulty];
  return {
    maxLives,
    currentLives: maxLives,
  };
}

/**
 * Decrease lives by 1 (on death)
 */
export function decreaseLives(lives: LivesSystem): LivesSystem {
  return {
    ...lives,
    currentLives: Math.max(0, lives.currentLives - 1),
  };
}

/**
 * Check if game is over (no lives remaining)
 */
export function isGameOver(lives: LivesSystem): boolean {
  return lives.currentLives <= 0;
}

// ============================================================
// NEW ARCHITECTURE - Player Types (Phase 1 Refactoring)
// ============================================================

/**
 * Player Persistent Data (Saved to storage)
 *
 * Contains all data that should be persisted between sessions.
 * This is the core identity and progress of the player.
 */
export interface PlayerPersistentData {
  /** Unique player ID */
  id: string;
  /** Player display name */
  name: string;
  /** Character class (swordsman/mage/summoner) */
  playerClass: CharacterClass;
  /** Class grade (e.g., "E", "D", "C", etc.) */
  classGrade: string;
  /** Player level */
  level: number;

  // Base stats (before equipment bonuses)
  /** Base maximum HP */
  baseMaxHp: number;
  /** Base maximum AP */
  baseMaxAp: number;
  /** Base speed */
  baseSpeed: number;

  // Deck (stored as Card IDs in actual save, but Card[] for runtime)
  /** Deck card IDs */
  deckCardIds: string[];

  // Titles/Achievements
  /** Earned titles */
  titles: string[];
}

/**
 * Player Resources
 *
 * Manages gold and magic stones with separation between
 * safe (baseCamp) and at-risk (exploration) resources.
 */
export interface PlayerResources {
  /** Gold stored safely at BaseCamp (kept on death) */
  baseCampGold: number;
  /** Gold gained during exploration (lost on death) */
  explorationGold: number;

  /** Magic stones stored safely at BaseCamp */
  baseCampMagicStones: MagicStones;
  /** Magic stones gained during exploration (lost on death) */
  explorationMagicStones: MagicStones;

  /** Exploration limit tracking */
  explorationLimit: ExplorationLimit;
}

/**
 * Player Inventory
 *
 * Manages all item storage systems.
 */
export interface PlayerInventory {
  /** Permanent storage at BaseCamp (max 100) */
  storage: StorageState;
  /** Carried items during exploration (max 20, lost on death) */
  inventory: InventoryState;
  /** Spare equipment for dungeon swaps (max 3, lost on death) */
  equipmentInventory: EquipmentInventoryState;
  /** Currently equipped items */
  equipmentSlots: EquipmentSlots;
}

/**
 * Player Progression
 *
 * Tracks unlocks, achievements, and game progress.
 */
export interface PlayerProgression {
  /** Sanctuary skill tree progress */
  sanctuaryProgress: SanctuaryProgress;
  /** Depths that have been unlocked */
  unlockedDepths: Depth[];
  /** Completed achievement IDs */
  completedAchievements: string[];
}

/**
 * Complete Player Data (Camp Time)
 *
 * Full player state used when in BaseCamp or outside of battle.
 * Combines all sub-types into a single comprehensive object.
 */
export interface PlayerData {
  persistent: PlayerPersistentData;
  resources: PlayerResources;
  inventory: PlayerInventory;
  progression: PlayerProgression;
}

/**
 * Player Battle State
 *
 * Runtime-only state used during battle.
 * Extends BattleStats with player-specific properties.
 */
export interface PlayerBattleState extends BattleStats {
  /** Card energy for playing cards */
  cardEnergy: number;
  /** Maximum card energy per turn */
  maxCardEnergy: number;
  /** Class-specific ability state */
  classAbility: ClassAbilityState;
  /** Current deck state (draw pile, hand, discard pile) */
  currentDeck: DeckState;

  // Equipment bonuses (calculated at battle start)
  /** Attack percentage bonus from equipment */
  equipmentAtkPercent: number;
  /** Defense percentage bonus from equipment */
  equipmentDefPercent: number;
}

// ============================================================
// LEGACY TYPES (Backward Compatibility)
// ============================================================

/**
 * @deprecated Use PlayerPersistentData + PlayerBattleState instead.
 * This type is kept for backward compatibility during migration.
 *
 * Player interface extending Character with player-specific properties.
 */
export interface Player {
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
  buffDebuffs: BuffDebuffMap;
  equipment?: string[];
  equipmentAtkPercent?: number;
  equipmentDefPercent?: number;
  tittle?: string[];
}

/**
 * @deprecated Use PlayerData instead.
 * This type is kept for backward compatibility during migration.
 *
 * Extended player with camp-time properties.
 */
export interface ExtendedPlayer extends Player {
  storage: StorageState;
  inventory: InventoryState;
  equipmentInventory: EquipmentInventoryState;
  equipmentSlots: EquipmentSlots;
  explorationGold: number;
  baseCampGold: number;
  explorationMagicStones: MagicStones;
  baseCampMagicStones: MagicStones;
  explorationLimit: ExplorationLimit;
  sanctuaryProgress: SanctuaryProgress;
}

// ============================================================
// HELPER FUNCTIONS
// ============================================================

/**
 * Create empty magic stones object
 */
export function createEmptyMagicStones(): MagicStones {
  return {
    small: 0,
    medium: 0,
    large: 0,
    huge: 0,
  };
}

/**
 * Create default exploration limit
 */
export function createDefaultExplorationLimit(): ExplorationLimit {
  return {
    current: 0,
    max: 10,
  };
}

/**
 * Create default sanctuary progress
 */
export function createDefaultSanctuaryProgress(): SanctuaryProgress {
  return {
    currentRunSouls: 0,
    totalSouls: 0,
    unlockedNodes: [],
    explorationLimitBonus: 0,
  };
}

/**
 * Calculate total gold (baseCamp + exploration)
 */
export function calculateTotalGold(resources: PlayerResources): number {
  return resources.baseCampGold + resources.explorationGold;
}

/**
 * Check if player can afford a cost
 */
export function canAfford(resources: PlayerResources, cost: number): boolean {
  return calculateTotalGold(resources) >= cost;
}
