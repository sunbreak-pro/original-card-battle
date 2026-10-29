/**
 * Character Utility Functions
 *
 * Factory functions for character-related state objects.
 */

import type { BuffDebuffMap } from '@/types';

/**
 * Creates a default empty BuffDebuffMap
 */
export function createEmptyBuffDebuffMap(): BuffDebuffMap {
  return new Map();
}
