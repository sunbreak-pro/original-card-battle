// Item type definitions for the BaseCamp system

/**
 * Type of item
 */
export type ItemType =
  | "equipment"
  | "consumable"
  | "magicStone"
  | "material"
  | "quest"
  | "key";

/**
 * Equipment slot types
 */
export type EquipmentSlot =
  | "weapon"
  | "armor"
  | "helmet"
  | "boots"
  | "accessory1"
  | "accessory2";

/**
 * Item rarity levels
 */
export type ItemRarity = "common" | "uncommon" | "rare" | "epic" | "legendary";

/**
 * Equipment quality levels (from Blacksmith system)
 */
export type EquipmentQuality = "poor" | "normal" | "good" | "master";

/**
 * Equipment effect types
 */
export interface EquipmentEffect {
  type: "stat" | "skill" | "passive";
  target: string; // e.g., "hp", "ap", "attack", "defense"
  value: number | string;
  description: string;
}

/**
 * Base item interface
 * Used for all item types in the game (equipment, consumables, materials, etc.)
 */
export interface Item {
  id: string; // Unique instance ID
  typeId: string; // Item type ID (for same items)
  name: string;
  description: string;
  itemType: ItemType;
  type: string; // Emoji or image path

  // Equipment-specific properties
  equipmentSlot?: EquipmentSlot;
  durability?: number; // Current durability (AP in equipment system)
  maxDurability?: number; // Max durability
  level?: number; // Equipment level (0-3)
  quality?: EquipmentQuality; // Equipment quality
  effects?: EquipmentEffect[];

  // Consumable-specific properties
  stackable?: boolean;
  stackCount?: number;
  maxStack?: number;

  // Magic Stone-specific properties
  magicStoneValue?: number; // Value in currency (30, 100, 350)
  magicStoneSize?: "small" | "medium" | "large";

  // Common properties
  rarity: ItemRarity;
  sellPrice: number; // Sell price in gold
  canSell: boolean;
  canDiscard: boolean;
}

/**
 * Magic Stone currency structure
 */
export interface MagicStones {
  small: number; // Value: 30 each
  medium: number; // Value: 100 each
  large: number; // Value: 350 each
}

/**
 * Helper function to calculate total magic stone value
 */
export function calculateMagicStoneValue(stones: MagicStones): number {
  return stones.small * 30 + stones.medium * 100 + stones.large * 350;
}

/**
 * Helper function to create a new item instance
 */
export function createItemInstance(
  typeId: string,
  baseItem: Omit<Item, "id">
): Item {
  return {
    ...baseItem,
    id: `${typeId}_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`,
  };
}
