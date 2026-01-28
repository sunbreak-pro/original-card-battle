/**
 * Card Execution Logic
 *
 * Factory functions for card execution results.
 */

import type { CardExecutionResult } from '@/types/battleTypes';

/**
 * Default card execution result
 */
export function createDefaultExecutionResult(): CardExecutionResult {
  return {
    success: false,
    damageDealt: 0,
    guardGained: 0,
    healingDone: 0,
    energyConsumed: 0,
    swordEnergyChange: 0,
    swordEnergyConsumed: 0,
    buffsApplied: [],
    debuffsApplied: [],
    cardsDrawn: 0,
    isCritical: false,
    lifestealAmount: 0,
    reflectDamage: 0,
    bleedDamage: 0,
  };
}
