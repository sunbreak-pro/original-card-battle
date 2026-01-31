/**
 * Enemy Encyclopedia Data
 *
 * Provides all enemies for the bestiary display.
 * Includes enemies from all 5 depths.
 */

import { enemyList as depth1Enemies } from "../characters/enemy/enemyDepth1";
import { enemyList as depth2Enemies } from "../characters/enemy/enemyDepth2";
import { enemyList as depth3Enemies } from "../characters/enemy/enemyDepth3";
import { enemyList as depth4Enemies } from "../characters/enemy/enemyDepth4";
import { enemyList as depth5Enemies } from "../characters/enemy/enemyDepth5";
import type { EnemyDefinition } from '@/types/characterTypes';
import type { EnemyEncyclopediaEntry } from '@/types/campTypes';
import type { Depth } from '@/types/cardTypes';

/** Enemy list indexed by depth */
const ENEMIES_BY_DEPTH: Record<Depth, EnemyDefinition[]> = {
  1: depth1Enemies,
  2: depth2Enemies,
  3: depth3Enemies,
  4: depth4Enemies,
  5: depth5Enemies,
};

/**
 * Get all available enemies for encyclopedia
 * Sorted by HP (ascending) for display order
 */
export function getAllEnemies(): EnemyDefinition[] {
  const all = [
    ...depth1Enemies,
    ...depth2Enemies,
    ...depth3Enemies,
    ...depth4Enemies,
    ...depth5Enemies,
  ];
  return all.sort((a, b) => a.baseMaxHp - b.baseMaxHp);
}

/**
 * Get enemies for a specific depth
 */
export function getEnemiesByDepth(depth: Depth): EnemyDefinition[] {
  return ENEMIES_BY_DEPTH[depth] ?? [];
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
export function isBossEnemy(enemy: EnemyDefinition): boolean {
  // Boss enemies typically have id ending with "boss" or HP > 100
  return enemy.id.includes("boss") || enemy.baseMaxHp > 100;
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

  const totalHp = enemies.reduce((sum, e) => sum + e.baseMaxHp, 0);

  return {
    total: enemies.length,
    normalCount: enemies.length - bosses.length,
    bossCount: bosses.length,
    avgHp: Math.round(totalHp / enemies.length),
  };
}
