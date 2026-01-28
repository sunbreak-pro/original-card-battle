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
 * Sell prices by rarity
 */
export const RARITY_SELL_PRICES: Record<ItemRarity, number> = {
    common: 50,
    uncommon: 100,
    rare: 150,
    epic: 400,
    legendary: 1000,
};