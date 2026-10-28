/**
 * Enemy Utility Functions
 *
 * Helper functions for enemy state management.
 */

import { generateId } from '@/utils/idGenerator';

/**
 * Generate unique enemy instance ID
 */
export function generateEnemyInstanceId(definitionId: string): string {
  return generateId(definitionId);
}
