/**
 * Item Utility Functions
 *
 * Helper functions for item calculations.
 */

import type { MagicStones } from '@/types/itemTypes';

/**
 * Calculate total magic stone value
 */
export function calculateMagicStoneValue(stones: MagicStones): number {
  return stones.small * 30 + stones.medium * 100 + stones.large * 350 + stones.huge * 1000;
}
