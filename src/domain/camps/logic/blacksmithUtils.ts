/**
 * Blacksmith Utility Functions
 *
 * Helper functions for blacksmith operations.
 * These were originally in BlacksmithTypes.ts.
 */

import type { Item } from '@/types/itemTypes';
import type { EquipmentQuality } from '@/types/itemTypes';

/** Quality order for progression */
export const QUALITY_ORDER: EquipmentQuality[] = ["poor", "normal", "good", "master"];

/** Maximum equipment level */
export const MAX_EQUIPMENT_LEVEL = 3;

/**
 * Get next quality tier
 */
export function getNextQuality(current: EquipmentQuality): EquipmentQuality | null {
  const index = QUALITY_ORDER.indexOf(current);
  if (index === -1 || index === QUALITY_ORDER.length - 1) return null;
  return QUALITY_ORDER[index + 1];
}

/**
 * Check if item is equipment
 */
export function isEquipment(item: Item): boolean {
  return item.itemType === "equipment";
}

/**
 * Check if equipment can be upgraded (level < max)
 */
export function canUpgradeLevel(item: Item): boolean {
  return isEquipment(item) && (item.level ?? 0) < MAX_EQUIPMENT_LEVEL;
}

/**
 * Check if equipment quality can be upgraded
 */
export function canUpgradeQuality(item: Item): boolean {
  return isEquipment(item) && item.quality !== undefined && item.quality !== "master";
}

/**
 * Check if equipment needs repair
 */
export function needsRepair(item: Item): boolean {
  return (
    isEquipment(item) &&
    item.durability !== undefined &&
    item.maxDurability !== undefined &&
    item.durability < item.maxDurability
  );
}

/**
 * Check if equipment can be dismantled
 */
export function canDismantle(item: Item): boolean {
  return isEquipment(item) && item.canDiscard;
}
