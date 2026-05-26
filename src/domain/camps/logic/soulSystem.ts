// Soul acquisition and management system

import type { SanctuaryProgress } from '@/types/campTypes';
import { SANCTUARY_CONSTANTS } from '@/constants/campConstants';
import { applySoulMultiplier } from "./sanctuaryLogic";
import type { MagicStones } from '@/types/itemTypes';

/**
 * Enemy types for soul calculation
 */
export type EnemyType = "single" | "double" | "three" | "boss";

/**
 * Return method types for survival calculation
 */
export type ReturnMethod = "early" | "normal" | "fullClear";

/**
 * Soul gain result
 */
export interface SoulGainResult {
  baseSouls: number;
  multiplierApplied: number;
  finalSouls: number;
  newProgress: SanctuaryProgress;
}

/**
 * Survival result
 */
export interface SurvivalResult {
  currentRunSouls: number;
  multiplier: number;
  transferredSouls: number;
  newTotalSouls: number;
  newProgress: SanctuaryProgress;
}

/**
 * Death result
 */
export interface DeathResult {
  lostSouls: number;
  newProgress: SanctuaryProgress;
}

/**
 * Calculate base soul value for an enemy type
 */
export function getBaseSoulValue(enemyType: EnemyType): number {
  switch (enemyType) {
    case "single":
      return SANCTUARY_CONSTANTS.SOUL_VALUES.normal;
    case "double":
    case "three":
      return SANCTUARY_CONSTANTS.SOUL_VALUES.elite;
    case "boss":
      return SANCTUARY_CONSTANTS.SOUL_VALUES.boss;
    default:
      return 0;
  }
}

/**
 * Get survival multiplier for return method
 */
function getSurvivalMultiplier(returnMethod: ReturnMethod): number {
  switch (returnMethod) {
    case "early":
      return SANCTUARY_CONSTANTS.SURVIVAL_MULTIPLIERS.earlyReturn;
    case "normal":
      return SANCTUARY_CONSTANTS.SURVIVAL_MULTIPLIERS.normalReturn;
    case "fullClear":
      return SANCTUARY_CONSTANTS.SURVIVAL_MULTIPLIERS.fullClear;
    default:
      return 0;
  }
}

/**
 * Gain souls from defeating an enemy
 * Returns a new SanctuaryProgress with updated currentRunSouls
 */
export function gainSoulFromEnemy(
  progress: SanctuaryProgress,
  enemyType: EnemyType,
  isReturnBattle: boolean = false
): SoulGainResult {
  // Get base soul value
  let baseSouls = getBaseSoulValue(enemyType);

  // Apply return battle multiplier if applicable
  if (isReturnBattle) {
    baseSouls = Math.floor(
      baseSouls * SANCTUARY_CONSTANTS.SOUL_VALUES.returnRouteMultiplier
    );
  }

  // Apply soul multiplier from sanctuary upgrades
  const finalSouls = applySoulMultiplier(baseSouls, progress);

  const newProgress: SanctuaryProgress = {
    ...progress,
    currentRunSouls: progress.currentRunSouls + finalSouls,
  };

  return {
    baseSouls,
    multiplierApplied: isReturnBattle
      ? SANCTUARY_CONSTANTS.SOUL_VALUES.returnRouteMultiplier
      : 1.0,
    finalSouls,
    newProgress,
  };
}

/**
 * Complete survival - transfer current run souls to total
 * Returns a new SanctuaryProgress with transferred souls
 */
export function completeSurvival(
  progress: SanctuaryProgress,
  returnMethod: ReturnMethod
): SurvivalResult {
  const multiplier = getSurvivalMultiplier(returnMethod);
  const transferredSouls = Math.floor(progress.currentRunSouls * multiplier);
  const newTotalSouls = progress.totalSouls + transferredSouls;

  const newProgress: SanctuaryProgress = {
    ...progress,
    currentRunSouls: 0,
    totalSouls: newTotalSouls,
  };

  return {
    currentRunSouls: progress.currentRunSouls,
    multiplier,
    transferredSouls,
    newTotalSouls,
    newProgress,
  };
}

/**
 * Handle player death - transfer 100% of current run souls to total
 * V3.0 Design: Souls are preserved on death (100% retention)
 * Returns a new SanctuaryProgress with transferred souls
 */
export function handleDeath(progress: SanctuaryProgress): DeathResult {
  const transferredSouls = progress.currentRunSouls;

  const newProgress: SanctuaryProgress = {
    ...progress,
    currentRunSouls: 0,
    // V3.0: Transfer all current run souls to total (100% retention)
    totalSouls: progress.totalSouls + transferredSouls,
  };

  return {
    lostSouls: 0, // V3.0: No souls are lost on death
    newProgress,
  };
}

/**
 * Calculate souls needed for next available upgrade
 * Returns the minimum cost of available nodes, or null if none available
 */
export function getSoulsNeededForNextUpgrade(
  progress: SanctuaryProgress,
  availableNodeCosts: number[]
): number | null {
  if (availableNodeCosts.length === 0) return null;

  const minCost = Math.min(...availableNodeCosts);
  const needed = minCost - progress.totalSouls;

  return needed > 0 ? needed : 0;
}

/**
 * Get soul value for a specific enemy type (simple getter)
 * Use this for VictoryScreen display
 */
export function getSoulValue(enemyType: EnemyType): number {
  return getBaseSoulValue(enemyType);
}

/**
 * Calculate magic stone drops based on enemy type
 * - Normal: small x1
 * - Elite: small x2, medium x1
 * - Boss: medium x1, large x1
 */
export function calculateMagicStoneDrops(enemyType: EnemyType): MagicStones {
  switch (enemyType) {
    case "boss":
      return { small: 0, medium: 1, large: 1, huge: 0 };
    case "double":
    case "three":
      return { small: 2, medium: 1, large: 0, huge: 0 };
    default:
      return { small: 1, medium: 0, large: 0, huge: 0 };
  }
}

/**
 * Format magic stone drops for display
 */
export function formatMagicStoneDrops(stones: MagicStones): string {
  const parts: string[] = [];
  if (stones.small > 0) parts.push(`小×${stones.small}`);
  if (stones.medium > 0) parts.push(`中×${stones.medium}`);
  if (stones.large > 0) parts.push(`大×${stones.large}`);
  if (stones.huge > 0) parts.push(`極大×${stones.huge}`);
  return parts.length > 0 ? parts.join(", ") : "なし";
}
