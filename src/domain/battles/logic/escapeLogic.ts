/**
 * Escape Logic
 *
 * Calculates escape probability and attempts escape from battle.
 */

/**
 * Calculate escape chance based on player and enemy speed.
 * Range: 10% - 95%
 */
export function calculateEscapeChance(playerSpeed: number, enemySpeed: number): number {
  return Math.min(0.95, Math.max(0.1, 0.4 * playerSpeed / Math.max(enemySpeed, 1)));
}

/**
 * Attempt to escape from battle.
 * Returns true if escape succeeds.
 */
export function attemptEscape(playerSpeed: number, enemySpeed: number): boolean {
  return Math.random() < calculateEscapeChance(playerSpeed, enemySpeed);
}
