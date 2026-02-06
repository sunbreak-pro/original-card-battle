/**
 * Camp Type Definitions
 *
 * Centralized camp-related types including:
 * - Facility types and navigation
 * - Shop, Guild, Blacksmith, Sanctuary, Library, Storage types
 * - NodeStatus (unified)
 */

import type { Item } from './itemTypes';
import type { EquipmentSlot, EquipmentQuality } from './itemTypes';
import type { Card, CardTag } from './cardTypes';
import type { Depth } from './cardTypes';
import type { ElementType } from './characterTypes';

// Re-export Depth from cardTypes for convenience
export type { Depth } from './cardTypes';

// ============================================================
// Core Camp Types
// ============================================================

export type FacilityType =
  | "guild"
  | "shop"
  | "blacksmith"
  | "sanctuary"
  | "storage"
  | "dungeon";

export type GameScreen =
  | "character_select"
  | "camp"
  | "battle"
  | "guild"
  | "shop"
  | "blacksmith"
  | "sanctuary"
  | "storage"
  | "dungeon"
  | "dungeon_map";

export type BattleMode =
  | "normal"
  | "exam"
  | "return_route"
  | null;

export interface BattleConfig {
  enemyIds: string[];
  backgroundType: "dungeon" | "arena" | "guild";
  enemyType?: "single" | "double" | "three" | "boss";
  onWin?: () => void;
  onLose?: () => void;
}

export interface ExplorationLimit {
  current: number;
  max: number;
}

export interface SanctuaryProgress {
  currentRunSouls: number;
  totalSouls: number;
  unlockedNodes: string[];
  explorationLimitBonus: number;
}

export interface ResourceTracking {
  gold: number;
  explorationGold: number;
  baseCampGold: number;
  magicStones: { small: number; medium: number; large: number; };
  explorationMagicStones: { small: number; medium: number; large: number; };
  baseCampMagicStones: { small: number; medium: number; large: number; };
}

export interface PlayerStatistics {
  totalExplorations: number;
  survivalCount: number;
  deathCount: number;
  deepestDepth: Depth;
  totalSoulsGained: number;
  avgSoulsPerRun: number;
  totalGoldEarned: number;
  totalMonstersDefeated: number;
}

export interface FacilityUnlockState {
  guild: boolean;
  shop: boolean;
  blacksmith: boolean;
  sanctuary: boolean;
  storage: boolean;
  dungeon: boolean;
}

// ============================================================
// Node Status (Unified)
// ============================================================

/**
 * Unified node status used across Dungeon and Sanctuary
 */
export type NodeStatus =
  | "locked"
  | "available"
  | "current"
  | "completed"
  | "skipped"
  | "unlocked";

// ============================================================
// Shop Types
// ============================================================

export type ShopTab = "buy" | "sell" | "exchange" | "dark_market";
export type ShopCategory = "consumable" | "teleport" | "battleItem" | "mapItem";

export interface ShopListing {
  itemTypeId: string;
  category: ShopCategory;
  stock?: number;
}

// ============================================================
// Shop Stock Types
// ============================================================

/**
 * Runtime shop stock state.
 * Tracks inventory counts, daily rotation, and restock timing.
 */
export interface ShopStockState {
  /** Permanent consumable stock: itemKey → remaining count */
  permanentStock: Record<string, number>;
  /** Daily special stock: itemKey → remaining count */
  dailySpecialStock: Record<string, number>;
  /** Set of equipment indices that have been purchased (sold out) */
  equipmentSoldOutIndices: number[];
  /** Current rotation's daily special item keys */
  dailySpecialKeys: string[];
  /** Battle count since last restock */
  battlesSinceRestock: number;
  /** Random threshold for next restock (7-10) */
  restockThreshold: number;
  /** Seed used for current daily rotation */
  rotationSeed: number;
  /** Whether new stock is available (for notification badge) */
  hasNewStock: boolean;

  // ===== Dark Market Fields =====
  /** Dark Market consumable stock: itemKey → remaining count */
  darkMarketConsumableStock: Record<string, number>;
  /** Dark Market equipment sold-out indices */
  darkMarketEquipmentSoldOutIndices: number[];
  /** Dark Market rotation seed (updated on boss defeat) */
  darkMarketSeed: number;
  /** Whether dark market has new items (boss defeated) */
  darkMarketHasNewStock: boolean;
}

// ============================================================
// Guild Types
// ============================================================

export interface PromotionExam {
  currentGrade: string;
  nextGrade: string;
  requiredCardCount: number;
  requiredGold?: number;
  enemyId: string;
  description: string;
  recommendations: { hp: number; ap: number; };
  rewards: {
    statBonus: string;
    maxHpBonus?: number;
    maxApBonus?: number;
    items?: string[];
  };
}

export type RumorEffect =
  | { type: "elite_rate"; value: number }
  | { type: "shop_discount"; value: number }
  | { type: "treasure_rate"; value: number }
  | { type: "start_bonus"; bonus: string };

export interface Rumor {
  id: string;
  name: string;
  description: string;
  cost: number;
  effect: RumorEffect;
  rarity: "common" | "rare" | "epic";
  icon: string;
  duration?: number;
}

export interface QuestObjective {
  type: "defeat" | "collect" | "explore" | "survive";
  target: string;
  required: number;
  current: number;
  description: string;
}

export interface QuestReward {
  gold?: number;
  magicStones?: number;
  items?: string[];
  experience?: number;
}

export interface Quest {
  id: string;
  title: string;
  description: string;
  type: "daily" | "weekly" | "story";
  requiredGrade: string;
  objectives: QuestObjective[];
  rewards: QuestReward;
  isActive: boolean;
  isCompleted: boolean;
  expiresAt?: Date;
}

export interface GuildState {
  activeRumors: string[];
  acceptedQuests: string[];
  completedQuests: string[];
  availableExam: PromotionExam | null;
  lastQuestRefresh?: Date;
}

// ============================================================
// Blacksmith Types
// ============================================================

export type BlacksmithTab = "upgrade" | "repair" | "dismantle";
export type QualityUpOption = "normal" | "qualityFocused" | "maxQuality";

export interface UpgradeCost {
  gold: number;
  magicStones: number;
}

export interface QualityUpConfig {
  option: QualityUpOption;
  label: string;
  description: string;
  costMultiplier: number;
  successRates: {
    poorToNormal: number;
    normalToGood: number;
    goodToMaster: number;
  };
}

export interface RepairCost {
  gold: number;
  durabilityRestored: number;
}

export interface DismantleResult {
  goldReturn: number;
  magicStoneChance: number;
  magicStoneReturn: number;
  bonusDescription: string;
}

export interface BlacksmithResult {
  success: boolean;
  message: string;
  item?: Item;
  goldChange?: number;
  magicStoneChange?: number;
  qualityChanged?: boolean;
}

export interface UpgradePreview {
  currentLevel: number;
  nextLevel: number;
  statChanges: { stat: string; before: number; after: number; }[];
  cost: UpgradeCost;
  canAfford: boolean;
}

export interface QualityUpgradePreview {
  currentQuality: EquipmentQuality;
  targetQuality: EquipmentQuality;
  successChance: number;
  cost: UpgradeCost;
  canAfford: boolean;
}

// ============================================================
// Sanctuary Types
// ============================================================

export type SkillCategory =
  | "hp"
  | "gold"
  | "combat"
  | "utility"
  | "class"
  | "exploration";

export type SkillTier = 1 | 2 | 3;

export type NodeEffectType =
  | "stat_boost"
  | "special_ability"
  | "resource_increase"
  | "exploration_limit"
  | "gold_multiplier"
  | "soul_multiplier"
  | "element_enhancement";

export interface NodeEffect {
  type: NodeEffectType;
  target: string;
  value: number;
  description: string;
}

export interface SkillNode {
  id: string;
  name: string;
  description: string;
  icon: string;
  cost: number;
  category: SkillCategory;
  tier: SkillTier;
  prerequisites: string[];
  effects: NodeEffect[];
  classRestriction?: CharacterClassForSanctuary;
  position: { angle: number; radius: number; };
}

/** Character class type used in sanctuary (same as CharacterClass) */
export type CharacterClassForSanctuary = "swordsman" | "mage";

export interface UnlockResult {
  success: boolean;
  message: string;
  remainingSouls?: number;
}

export interface SanctuaryEffects {
  initialHpBonus: number;
  goldMultiplier: number;
  soulMultiplier: number;
  explorationLimitBonus: number;
  inventoryBonus: number;
  hpRecoveryPercent: number;
  hasAppraisal: boolean;
  hasTrueAppraisal: boolean;
  hasIndomitableWill: boolean;
  classEnergy: {
    swordsman: number;
    mage: number;
  };
  enhancedElements: Set<ElementType>;
}

export interface EnemySoulValues {
  normal: number;
  elite: number;
  boss: number;
  returnRoute: number;
}

export interface SurvivalMultipliers {
  earlyReturn: number;
  normalReturn: number;
  fullClear: number;
}

// ============================================================
// Encyclopedia & Tab Types
// ============================================================

export type GuildTab = "promotion" | "rumors" | "quests" | "storage";
export type PreparationTab = "deck" | "inventory" | "equipment";
export interface CardEncyclopediaEntry {
  card: Card;
  isUnlocked: boolean;
  timesUsed: number;
  firstObtainedDate?: string;
}

export interface EnemyEncyclopediaEntry {
  enemy: import('./characterTypes').EnemyDefinition;
  isEncountered: boolean;
  timesDefeated: number;
  firstEncounteredDate?: string;
}

export type TipCategory = "battle" | "cards" | "exploration" | "class" | "general";

export interface GameTip {
  id: string;
  category: TipCategory;
  title: string;
  content: string;
  isUnlocked: boolean;
}

export interface CardFilterOptions {
  element: ElementType | null;
  characterClass: string | null;
  searchText: string;
  tag: CardTag | null;
  costMin: number | null;
  costMax: number | null;
  showUnknown: boolean;
}

export interface EnemyFilterOptions {
  depth: number | null;
  isBoss: boolean | null;
  searchText: string;
}

// ============================================================
// Storage Types
// ============================================================

export interface StorageState {
  items: Item[];
  maxCapacity: number;
  currentCapacity: number;
}

export interface InventoryState {
  items: Item[];
  maxCapacity: number;
  currentCapacity: number;
}

export interface EquipmentInventoryState {
  items: Item[];
  maxCapacity: number;
  currentCapacity: number;
}

export interface EquipmentSlots {
  weapon: Item | null;
  armor: Item | null;
  helmet: Item | null;
  boots: Item | null;
  accessory1: Item | null;
  accessory2: Item | null;
}

export type MoveDirection =
  | "storage_to_inventory"
  | "inventory_to_storage"
  | "storage_to_equipment"
  | "equipment_to_storage"
  | "storage_to_equipment_inventory"
  | "equipment_inventory_to_storage"
  | "equipment_inventory_to_equipment"
  | "equipment_to_equipment_inventory";

export interface MoveResult {
  success: boolean;
  message: string;
  movedItem?: Item;
  replacedItem?: Item;
}

export interface EquipmentSet {
  id: string;
  name: string;
  equipment: {
    weapon?: string;
    armor?: string;
    helmet?: string;
    boots?: string;
    accessory1?: string;
    accessory2?: string;
  };
}

export interface ItemFilter {
  itemType?: "equipment" | "consumable" | "magicStone" | "material";
  rarity?: "common" | "uncommon" | "rare" | "epic" | "legendary";
  equipmentSlot?: EquipmentSlot;
  searchTerm?: string;
}

export type ItemSortCriteria =
  | "rarity"
  | "level"
  | "name"
  | "type"
  | "recent";

