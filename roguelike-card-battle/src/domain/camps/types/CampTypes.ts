// Common BaseCamp and facility type definitions

/**
 * Facility types in BaseCamp
 */
export type FacilityType =
  | "guild" // Guild/Tavern (Promotion, Rumors, Quests)
  | "shop" // Shop (Buy/Sell items)
  | "blacksmith" // Blacksmith (Upgrade, Repair, Dismantle)
  | "sanctuary" // Sanctuary (Permanent upgrades via souls)
  | "library" // Library (Deck building, Encyclopedia, Records)
  | "storage" // Storage/Warehouse (Item management)
  | "dungeon"; // Dungeon Gate (Exploration entrance)

/**
 * Screen types for navigation
 */
export type GameScreen =
  | "camp" // BaseCamp main screen
  | "battle" // Battle screen
  | "guild"
  | "shop"
  | "blacksmith"
  | "sanctuary"
  | "library"
  | "storage"
  | "dungeon"
  | "dungeon_map"; // Dungeon node map during exploration

/**
 * Battle mode types
 */
export type BattleMode =
  | "normal" // Normal dungeon exploration
  | "exam" // Promotion exam battle
  | "return_route" // Return route encounter
  | null;

/**
 * Dungeon depth levels
 */
export type Depth = 1 | 2 | 3 | 4 | 5;

/**
 * Battle configuration
 * Defines battle parameters when starting a battle
 */
export interface BattleConfig {
  enemyIds: string[]; // Specific enemy IDs to spawn
  backgroundType: "dungeon" | "arena" | "guild"; // Battle background
  onWin?: () => void; // Callback on victory
  onLose?: () => void; // Callback on defeat
}

/**
 * Exploration limit tracking
 */
export interface ExplorationLimit {
  current: number; // Current exploration count
  max: number; // Max explorations allowed (default 10 + sanctuary bonuses)
}

/**
 * Sanctuary progression tracking
 */
export interface SanctuaryProgress {
  currentRunSouls: number; // Souls gained during current run (lost on death)
  totalSouls: number; // Total accumulated souls (permanent)
  unlockedNodes: string[]; // IDs of unlocked skill tree nodes
  explorationLimitBonus: number; // Bonus to max explorations from sanctuary
}

/**
 * Resource tracking (Gold and Magic Stones)
 * Separated into exploration (temporary) and baseCamp (permanent)
 */
export interface ResourceTracking {
  // Gold
  gold: number; // Total gold (exploration + baseCamp)
  explorationGold: number; // Gold gained during exploration (lost on death)
  baseCampGold: number; // Gold stored at BaseCamp (kept on death)

  // Magic Stones
  magicStones: {
    small: number;
    medium: number;
    large: number;
  };
  explorationMagicStones: {
    small: number;
    medium: number;
    large: number;
  };
  baseCampMagicStones: {
    small: number;
    medium: number;
    large: number;
  };
}

/**
 * Player statistics for tracking progress
 */
export interface PlayerStatistics {
  totalExplorations: number; // Total number of explorations
  survivalCount: number; // Number of successful survivals
  deathCount: number; // Number of deaths
  deepestDepth: Depth; // Deepest depth reached
  totalSoulsGained: number; // Total souls gained across all runs
  avgSoulsPerRun: number; // Average souls per run
  totalGoldEarned: number; // Total gold earned
  totalMonstersDefeated: number; // Total monsters defeated
}

/**
 * Facility unlock state
 */
export interface FacilityUnlockState {
  guild: boolean;
  shop: boolean;
  blacksmith: boolean;
  sanctuary: boolean;
  library: boolean;
  storage: boolean;
  dungeon: boolean;
}

/**
 * Helper function to check if exploration limit is exceeded
 */
export function isExplorationLimitExceeded(limit: ExplorationLimit): boolean {
  return limit.current >= limit.max;
}

/**
 * Helper function to get remaining explorations
 */
export function getRemainingExplorations(limit: ExplorationLimit): number {
  return Math.max(0, limit.max - limit.current);
}

/**
 * Helper function to calculate survival rate
 */
export function calculateSurvivalRate(stats: PlayerStatistics): number {
  if (stats.totalExplorations === 0) return 0;
  return (stats.survivalCount / stats.totalExplorations) * 100;
}
