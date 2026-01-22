/**
 * Enemy Encyclopedia Data
 *
 * Provides all enemies for the bestiary display.
 * Currently only Depth 1 enemies are available.
 */

import { enemyList } from "../../characters/enemy/data/enemyDepth1";
import type { Enemy } from "../../characters/type/enemyType";
import type { EnemyEncyclopediaEntry } from "../types/LibraryTypes";

/**
 * Get all available enemies for encyclopedia
 * Sorted by HP (ascending) for display order
 */
export function getAllEnemies(): Enemy[] {
  return [...enemyList].sort((a, b) => a.maxHp - b.maxHp);
}

/**
 * Create encyclopedia entries from enemies
 * All enemies are visible (encountered) for now
 */
export function createEnemyEncyclopediaEntries(): EnemyEncyclopediaEntry[] {
  const enemies = getAllEnemies();

  return enemies.map((enemy) => ({
    enemy,
    isEncountered: true, // All enemies are visible in encyclopedia
    timesDefeated: 0,
    firstEncounteredDate: undefined,
  }));
}

/**
 * Check if enemy is a boss
 */
export function isBossEnemy(enemy: Enemy): boolean {
  // Boss enemies typically have id ending with "boss" or HP > 100
  return enemy.id.includes("boss") || enemy.maxHp > 100;
}

/**
 * Get enemies filtered by boss status
 */
export function getEnemiesByBossStatus(
  isBoss: boolean | null
): EnemyEncyclopediaEntry[] {
  const entries = createEnemyEncyclopediaEntries();

  if (isBoss === null) {
    return entries;
  }

  return entries.filter((entry) => isBossEnemy(entry.enemy) === isBoss);
}

/**
 * Search enemies by name or description
 */
export function searchEnemies(searchText: string): EnemyEncyclopediaEntry[] {
  const entries = createEnemyEncyclopediaEntries();
  const lowerSearch = searchText.toLowerCase();

  if (!searchText) {
    return entries;
  }

  return entries.filter(
    (entry) =>
      entry.enemy.name.toLowerCase().includes(lowerSearch) ||
      (entry.enemy.nameJa?.toLowerCase().includes(lowerSearch) ?? false) ||
      entry.enemy.description.toLowerCase().includes(lowerSearch)
  );
}

/**
 * Get enemy statistics
 */
export function getEnemyStats(): {
  total: number;
  normalCount: number;
  bossCount: number;
  avgHp: number;
} {
  const enemies = getAllEnemies();
  const bosses = enemies.filter(isBossEnemy);

  const totalHp = enemies.reduce((sum, e) => sum + e.maxHp, 0);

  return {
    total: enemies.length,
    normalCount: enemies.length - bosses.length,
    bossCount: bosses.length,
    avgHp: Math.round(totalHp / enemies.length),
  };
}
