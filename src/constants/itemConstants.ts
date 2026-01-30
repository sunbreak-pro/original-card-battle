import type { EquipmentSlot } from '@/types/itemTypes';
import type { ItemRarity } from '@/types/itemTypes';
/**
 * Equipment slots for pack generation
 */
export const EQUIPMENT_SLOTS: EquipmentSlot[] = [
    "weapon",
    "armor",
    "helmet",
    "boots",
    "accessory1",
    "accessory2",
];

/**
 * Magic stone gold values by size
 */
export const MAGIC_STONE_VALUES = {
  small: 30,
  medium: 100,
  large: 350,
  huge: 1000,
} as const;

/**
 * Sell prices by rarity
 */
export const RARITY_SELL_PRICES: Record<ItemRarity, number> = {
    common: 50,
    uncommon: 100,
    rare: 150,
    epic: 400,
    legendary: 1000,
};

/**
 * Buy prices for individual equipment by rarity
 */
export const EQUIPMENT_BUY_PRICES: Record<ItemRarity, number> = {
    common: 120,
    uncommon: 250,
    rare: 400,
    epic: 900,
    legendary: 2500,
};