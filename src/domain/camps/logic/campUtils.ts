/**
 * Camp Utility Functions
 *
 * Helper functions for camp facility state management.
 */

import type { ExplorationLimit, PlayerStatistics } from '@/types/campTypes';

/**
 * Check if exploration limit is exceeded
 */
export function isExplorationLimitExceeded(limit: ExplorationLimit): boolean {
  return limit.current >= limit.max;
}

/**
 * Get remaining explorations
 */
export function getRemainingExplorations(limit: ExplorationLimit): number {
  return Math.max(0, limit.max - limit.current);
}

/**
 * Calculate survival rate
 */
export function calculateSurvivalRate(stats: PlayerStatistics): number {
  if (stats.totalExplorations === 0) return 0;
  return (stats.survivalCount / stats.totalExplorations) * 100;
}
