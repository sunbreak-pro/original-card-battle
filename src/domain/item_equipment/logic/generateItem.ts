import type { Item, ItemRarity } from '@/types/itemTypes';
import { EQUIPMENT_TEMPLATES } from "../../../constants/data/items/EquipmentData";
import { getConsumableData } from "@/constants/data/items/ConsumableItemData";
import type { EquipmentSlot } from '@/types/itemTypes';
import { RARITY_SELL_PRICES } from "../../../constants/itemConstants";
import { generateId } from '@/utils/idGenerator';

/**
 * Helper function to create a new item instance
 */
export function createItemInstance(
    typeId: string,
    baseItem: Omit<Item, "id">
): Item {
    return {
        ...baseItem,
        id: generateId(typeId),
    };
}

/**
 * Generate a consumable Item from ConsumableItemData by typeId
 * Returns null if the typeId is not found
 */
export function generateConsumableFromData(typeId: string): Item | null {
    const data = getConsumableData(typeId);
    if (!data) return null;

    const itemType = typeId === "teleport_stone" ? "key" : "consumable";

    return createItemInstance(typeId, {
        typeId: data.typeId,
        name: data.name,
        description: data.description,
        itemType,
        type: data.icon,
        rarity: data.rarity,
        sellPrice: data.sellPrice,
        canSell: true,
        canDiscard: true,
        stackable: true,
        stackCount: 1,
        maxStack: data.maxStack,
    });
}

/**
 * Generate equipment from a slot and rarity
 */
export function generateEquipmentItem(slot: EquipmentSlot, rarity: ItemRarity): Item {
    const template = EQUIPMENT_TEMPLATES[slot][rarity];

    return createItemInstance(`${slot}_${rarity}`, {
        typeId: `${slot}_${rarity}`,
        name: template.name,
        description: `A ${rarity} ${slot} piece of equipment.`,
        itemType: "equipment",
        type: template.icon,
        equipmentSlot: slot,
        rarity: rarity,
        quality: "normal",
        level: 0,
        durability: 100,
        maxDurability: 100,
        sellPrice: RARITY_SELL_PRICES[rarity],
        canSell: true,
        canDiscard: true,
        effects: [],
    });
}
