/**
 * Enemy Utility Functions
 *
 * Helper functions for enemy state management.
 */

import type { EnemyBattleState } from '@/types/characterTypes';

/**
 * Generate unique enemy instance ID
 */
export function generateEnemyInstanceId(definitionId: string): string {
  return `${definitionId}_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
}

/**
 * Check if enemy is alive
 */
export function isEnemyAlive(enemy: EnemyBattleState): boolean {
  return enemy.hp > 0;
}

/**
 * Get enemy HP percentage
 */
export function getEnemyHpPercent(enemy: EnemyBattleState): number {
  return (enemy.hp / enemy.maxHp) * 100;
}
