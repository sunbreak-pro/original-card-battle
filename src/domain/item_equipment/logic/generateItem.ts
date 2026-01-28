import type { Item } from "../type/ItemTypes";

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

export function generateConsumableItem(shopItem: ShopItem): Item {
    // Include heal amount in description for runtime use
    const description = shopItem.healAmount
        ? `${shopItem.description} (Heals ${shopItem.healAmount} HP)`
        : shopItem.description;

    return createItemInstance(shopItem.id, {
        typeId: shopItem.id,
        name: shopItem.name,
        description: description,
        itemType: "consumable",
        type: shopItem.icon,
        rarity: "common",
        sellPrice: Math.floor(shopItem.price * 0.5),
        canSell: true,
        canDiscard: true,
        stackable: true,
        stackCount: 1,
        maxStack: 99,
    });
}

/**
 * Create teleport item from shop item
 */
export function generateTeleportItem(shopItem: ShopItem): Item {
    return createItemInstance(shopItem.id, {
        typeId: shopItem.id,
        name: shopItem.name,
        description: shopItem.description,
        itemType: "key",
        type: shopItem.icon,
        rarity: "uncommon",
        sellPrice: Math.floor(shopItem.price * 0.5),
        canSell: true,
        canDiscard: true,
        stackable: true,
        stackCount: 1,
        maxStack: 10,
    });
}
