/**
 * Enemy Encyclopedia Data for Journal
 *
 * Re-exports enemy encyclopedia data from camps.
 * This module provides a stable import path for the journal system.
 */

export {
  getAllEnemies,
  getEnemiesByDepth,
  createEnemyEncyclopediaEntries,
  isBossEnemy,
  getEnemiesByBossStatus,
  searchEnemies,
  getEnemyStats,
} from "../camps/EnemyEncyclopediaData";
