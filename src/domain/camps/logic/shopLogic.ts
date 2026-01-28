// Shop transaction logic

import { type Item, type ItemRarity, type MagicStones } from "../../item_equipment/type/ItemTypes";
import { createItemInstance } from "../../item_equipment/logic/createItem";
import type { ShopItem, EquipmentPackConfig } from "../types/ShopTypes";
import { getShopItemById, getEquipmentPackById } from "../data/ShopData";
import type { EquipmentSlot } from "../../item_equipment/type/EquipmentType";
import { EQUIPMENT_TEMPLATES } from "../../item_equipment/data/EquipmentData";
/**
 * Equipment slots for pack generation
 */
const EQUIPMENT_SLOTS: EquipmentSlot[] = [
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
const RARITY_SELL_PRICES: Record<ItemRarity, number> = {
  common: 50,
  uncommon: 100,
  rare: 150,
  epic: 400,
  legendary: 1000,
};

/**
 * Check if player can afford an item
 */
export function canAfford(playerGold: number, price: number): boolean {
  return playerGold >= price;
}

/**
 * Check if inventory has space
 */
export function hasInventorySpace(
  currentCapacity: number,
  maxCapacity: number,
  itemCount = 1
): boolean {
  return currentCapacity + itemCount <= maxCapacity;
}

/**
 * Create consumable item from shop item
 */
export function createConsumableItem(shopItem: ShopItem): Item {
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
export function createTeleportItem(shopItem: ShopItem): Item {
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

/**
 * Roll rarity based on pack probabilities
 */
function rollRarity(pack: EquipmentPackConfig): ItemRarity {
  const roll = Math.random();
  const probs = pack.rarityProbabilities;

  let cumulative = 0;
  if ((cumulative += probs.common) > roll) return "common";
  if ((cumulative += probs.uncommon) > roll) return "uncommon";
  if ((cumulative += probs.rare) > roll) return "rare";
  if ((cumulative += probs.epic) > roll) return "epic";
  return "legendary";
}

/**
 * Generate equipment from a slot and rarity
 */
function generateEquipmentItem(slot: EquipmentSlot, rarity: ItemRarity): Item {
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

/**
 * Open equipment pack and generate items
 */
export function openEquipmentPack(packId: string): Item[] {
  const pack = getEquipmentPackById(packId);
  if (!pack) return [];

  const items: Item[] = [];

  for (const slot of EQUIPMENT_SLOTS) {
    const rarity = rollRarity(pack);
    const item = generateEquipmentItem(slot, rarity);
    items.push(item);
  }

  return items;
}

/**
 * Calculate sell price for an item
 */
export function calculateSellPrice(item: Item): number {
  return item.sellPrice;
}

/**
 * Calculate total magic stone value
 */
export function calculateMagicStoneTotal(stones: MagicStones): number {
  return stones.small * 30 + stones.medium * 100 + stones.large * 350;
}

/**
 * Calculate stones to consume for a given gold value
 * Returns the new stone counts after consumption
 * Consumes smallest stones first
 */
export function calculateStonesToConsume(
  stones: MagicStones,
  targetValue: number
): { newStones: MagicStones; actualValue: number } | null {
  const totalValue = calculateMagicStoneTotal(stones);
  if (totalValue < targetValue) return null;

  let remaining = targetValue;
  const newStones: MagicStones = { ...stones };

  // Consume small stones first (30 each)
  while (remaining > 0 && newStones.small > 0) {
    newStones.small--;
    remaining -= 30;
  }

  // Then medium stones (100 each)
  while (remaining > 0 && newStones.medium > 0) {
    newStones.medium--;
    remaining -= 100;
  }

  // Finally large stones (350 each)
  while (remaining > 0 && newStones.large > 0) {
    newStones.large--;
    remaining -= 350;
  }

  // Calculate actual value consumed (may be more than target due to stone denominations)
  const consumedValue = calculateMagicStoneTotal(stones) - calculateMagicStoneTotal(newStones);

  return {
    newStones,
    actualValue: consumedValue,
  };
}

/**
 * Get item by shop ID and create instance
 */
export function purchaseShopItem(shopItemId: string): Item | null {
  const shopItem = getShopItemById(shopItemId);
  if (!shopItem) return null;

  if (shopItem.category === "consumable") {
    return createConsumableItem(shopItem);
  } else if (shopItem.category === "teleport") {
    return createTeleportItem(shopItem);
  }
  else if (shopItem.category === "battleItem") {
    // return createEquipemnt(); Not implemented.
    return null;
  }

  return null;
}
