/**
 * Character Type Definitions
 *
 * Centralized character-related types including:
 * - CharacterClass and CardCharacterClass
 * - BattleStats
 * - Class ability types (Swordsman, Mage)
 * - Enemy types
 * - Player types
 */

import type { BuffDebuffMap, BuffDebuffState, BuffDebuffType } from './battleTypes';
import type { ExplorationLimit, SanctuaryProgress, Depth, ShopStockState } from './campTypes';
import type { MagicStones } from './itemTypes';
import type { EquipmentSlots, InventoryState, StorageState, EquipmentInventoryState } from './campTypes';
import type { DeckState } from '../domain/cards/decks/deckReducter';
import type React from "react";

// ============================================================
// Base Character Types
// ============================================================

/**
 * Character class types
 * - swordsman: Physical combat specialist with Sword Energy system
 * - mage: Magic specialist with Elemental Chain system
 */
export type CharacterClass = "swordsman" | "mage";

/**
 * Extended character class including common cards
 * Used for Card type to indicate which class can use the card
 */
export type CardCharacterClass = CharacterClass | "common";

/**
 * Battle Statistics Interface
 *
 * Common combat stats shared between Player and Enemy during battle.
 */
export interface BattleStats {
  /** Current hit points */
  hp: number;
  /** Maximum hit points */
  maxHp: number;
  /** Current armor points (damage reduction) */
  ap: number;
  /** Maximum armor points */
  maxAp: number;
  /** Current guard value (temporary shield) */
  guard: number;
  /** Speed determines action order */
  speed: number;
  /** Active buff/debuff effects */
  buffDebuffs: BuffDebuffMap;
}

// ============================================================
// Swordsman: Sword Energy System
// ============================================================

/**
 * Sword Energy State
 */
export interface SwordEnergyState {
  /** Discriminant for union type */
  type: "swordEnergy";
  /** Current sword energy (0-10) */
  current: number;
  /** Maximum sword energy (10) */
  max: number;
}

// ============================================================
// Mage: Elemental Chain System
// ============================================================

/**
 * Element types for all classes
 * - Magic: fire, ice, lightning, dark, light (Mage primary)
 * - Physical: physics, guard (Swordsman primary)
 * - Utility: buff, debuff, heal
 */
export type ElementType =
  | "fire" | "ice" | "lightning" | "dark" | "light"
  | "physics" | "slash" | "impact" | "guard"
  | "buff" | "debuff" | "heal"
  | "attack" | "classAbility" | "chain";

/**
 * Resonance level for elemental chain effects
 * - 0: No resonance (base effects)
 * - 1: First resonance (damage +15%, element-specific effect)
 * - 2: Maximum resonance (damage +30%, stronger effect + field buff)
 */
export type ResonanceLevel = 0 | 1 | 2;

/**
 * Elemental State for Mage class
 */
export interface ElementalState {
  /** Discriminant for union type */
  type: "elemental";
  /** Last element used (for resonance tracking) */
  lastElement: ElementType | null;
  /** Current resonance level (0-2) */
  resonanceLevel: ResonanceLevel;
}

/** Union type for all class ability states */
export type ClassAbilityState = SwordEnergyState | ElementalState;

// ============================================================
// Encounter Pattern Types
// ============================================================

/** Encounter size categories */
export type EncounterSize = "single" | "double" | "three" | "boss";

/** Fixed enemy encounter pattern */
export interface EncounterPattern {
  id: string;
  nameJa: string;
  enemies: EnemyDefinition[];
}

/** Depth-level enemy data organized by encounter size */
export interface DepthEnemyData {
  single: EncounterPattern[];
  double: EncounterPattern[];
  three: EncounterPattern[];
  boss: EnemyDefinition;
}

// ============================================================
// Enemy Types
// ============================================================

/** Enemy action types */
export type EnemyActionType = "attack" | "buff" | "debuff" | "special";

/** Enemy action definition */
export interface EnemyAction {
  name: string;
  type: EnemyActionType;
  baseDamage: number;
  /** Element types for this action. Defaults based on action type if not specified. */
  element?: ElementType[];
  applyDebuffs?: BuffDebuffState[];
  applyBuffs?: BuffDebuffState[];
  guardGain?: number;
  hitCount?: number;
  displayIcon?: string;
  priority?: number;
  energyCost?: number;
}

/** Enemy AI pattern definition */
export interface EnemyAIPattern {
  phaseNumber: number;
  condition?: (hp: number, maxHp: number) => boolean;
  action: EnemyAction;
  probability?: number;
}

/**
 * Enemy Definition (Master Data)
 */
export interface EnemyDefinition {
  /** Unique enemy type ID */
  id: string;
  /** Display name (English) */
  name: string;
  /** Display name (Japanese) */
  nameJa: string;
  /** Enemy description */
  description: string;

  // Base stats
  baseMaxHp: number;
  baseMaxAp: number;
  baseSpeed: number;
  startingGuard: boolean;

  // AI
  actEnergy: number;
  aiPatterns: EnemyAIPattern[];

  // Display
  imagePath?: string;
  /** Display width in vw units (Minion: 14-16, Standard: 18-20, Elite: 22-24, Boss: 28-32) */
  displayWidth?: number;
}

/**
 * Enemy Battle State
 */
export interface EnemyBattleState extends BattleStats {
  instanceId: string;
  definitionId: string;
  definition: EnemyDefinition;
  energy: number;
  phaseCount: number;
  turnCount: number;
  ref: React.RefObject<HTMLDivElement | null>;
}

// ============================================================
// Player Types
// ============================================================

/** Game difficulty affecting lives and other mechanics */
export type Difficulty = 'easy' | 'normal' | 'hard';

/** Lives system configuration */
export interface LivesSystem {
  maxLives: number;
  currentLives: number;
}

/**
 * Player Persistent Data (Saved to storage)
 */
export interface PlayerPersistentData {
  id: string;
  name: string;
  playerClass: CharacterClass;
  classGrade: string;
  level: number;
  baseMaxHp: number;
  baseMaxAp: number;
  baseSpeed: number;
  cardActEnergy: number;
  deckCardIds: string[];
  titles: string[];
}

/**
 * Player Resources
 */
export interface PlayerResources {
  baseCampGold: number;
  explorationGold: number;
  baseCampMagicStones: MagicStones;
  explorationMagicStones: MagicStones;
  explorationLimit: ExplorationLimit;
}

/**
 * Player Inventory
 */
export interface PlayerInventory {
  storage: StorageState;
  inventory: InventoryState;
  equipmentInventory: EquipmentInventoryState;
  equipmentSlots: EquipmentSlots;
}

/**
 * Player Progression
 */
export interface PlayerProgression {
  sanctuaryProgress: SanctuaryProgress;
  unlockedDepths: Depth[];
  completedAchievements: string[];
  shopRotationDay?: number;
  shopStockState?: ShopStockState;
}

/**
 * Complete Player Data (Camp Time)
 */
export interface PlayerData {
  persistent: PlayerPersistentData;
  resources: PlayerResources;
  inventory: PlayerInventory;
  progression: PlayerProgression;
}

/**
 * Player Battle State
 */
export interface PlayerBattleState extends BattleStats {
  cardEnergy: number;
  maxCardEnergy: number;
  classAbility: ClassAbilityState;
  currentDeck: DeckState;
  equipmentAtkPercent: number;
  equipmentDefPercent: number;
}

// ============================================================
// Resonance Effect Configuration
// ============================================================

/** Resonance effect configuration */
export interface ResonanceEffectConfig {
  burn?: { stacks: number; duration: number };
  freeze?: { duration: number };
  stun?: { duration: number };
  lifesteal?: number;
  cleanse?: number;
  heal?: number;
  weakness?: { duration: number };
  fieldBuff?: BuffDebuffType;
}
