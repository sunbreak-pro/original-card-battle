/**
 * Item Utility Functions
 *
 * Helper functions for item calculations.
 */

import type { MagicStones } from '@/types/itemTypes';
import { MAGIC_STONE_VALUES } from '@/constants/itemConstants';

/**
 * Calculate total magic stone value
 */
export function calculateMagicStoneValue(stones: MagicStones): number {
  return stones.small * MAGIC_STONE_VALUES.small
    + stones.medium * MAGIC_STONE_VALUES.medium
    + stones.large * MAGIC_STONE_VALUES.large
    + stones.huge * MAGIC_STONE_VALUES.huge;
}
