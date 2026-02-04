/**
 * Phase Queue Helper Functions
 *
 * Pure functions for phase queue manipulation in multi-enemy battles.
 * Extracted from useBattleOrchestrator for better modularity.
 */

import type { PhaseQueue, PhaseEntry } from '@/types/battleTypes';
import type { EnemyBattleState, BattleStats } from '@/types/characterTypes';

/**
 * Expand phase queue entries so each alive enemy gets its own turn
 * within each "enemy" phase slot.
 *
 * For a single enemy, this is a no-op (same as before).
 * For multiple enemies, each "enemy" entry is expanded to one entry per alive enemy.
 *
 * @param queue - The base phase queue (player vs single enemy)
 * @param allEnemies - All enemies array (to find alive ones by index)
 */
export function expandPhaseEntriesForMultipleEnemies(
  queue: PhaseQueue,
  allEnemies: EnemyBattleState[]
): PhaseEntry[] {
  // Find indices of alive enemies
  const aliveIndices = allEnemies
    .map((e, i) => ({ alive: e.hp > 0, index: i }))
    .filter(item => item.alive)
    .map(item => item.index);

  if (aliveIndices.length <= 1) {
    // Single enemy: just use the standard entries
    return queue.entries;
  }

  const expanded: PhaseEntry[] = [];
  for (const entry of queue.entries) {
    if (entry.actor === "player") {
      expanded.push(entry);
    } else {
      // Each alive enemy gets a turn in this enemy phase slot
      for (const idx of aliveIndices) {
        expanded.push({
          actor: "enemy",
          enemyIndex: idx,
        });
      }
    }
  }
  return expanded;
}

/**
 * Build BattleStats for a specific enemy
 */
export function buildEnemyBattleStats(enemy: EnemyBattleState): BattleStats {
  return {
    hp: enemy.hp,
    maxHp: enemy.maxHp,
    ap: enemy.ap,
    maxAp: enemy.maxAp,
    guard: enemy.guard,
    speed: enemy.definition.baseSpeed,
    buffDebuffs: enemy.buffDebuffs,
  };
}
