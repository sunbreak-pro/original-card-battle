/**
 * Enemy Encyclopedia Data for Journal
 *
 * Re-exports enemy encyclopedia data from camps.
 * This module provides a stable import path for the journal system.
 */

export {
  getAllEnemies,
  createEnemyEncyclopediaEntries,
  isBossEnemy,
  getEnemyStats,
} from "../camps/EnemyEncyclopediaData";
