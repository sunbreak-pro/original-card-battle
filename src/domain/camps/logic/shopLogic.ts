// Shop transaction logic

import type { Item, ItemRarity, MagicStones } from '@/types/itemTypes';
import { generateConsumableFromData, generateEquipmentItem } from "../../item_equipment/logic/generateItem";
import { EQUIPMENT_SLOTS, MAGIC_STONE_VALUES } from "../../../constants/itemConstants";
import type { EquipmentPackConfig, ShopListing } from '@/types/campTypes';
import { getEquipmentPackById, resolveShopListing } from "../data/ShopData";

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
  return stones.small * MAGIC_STONE_VALUES.small
    + stones.medium * MAGIC_STONE_VALUES.medium
    + stones.large * MAGIC_STONE_VALUES.large
    + stones.huge * MAGIC_STONE_VALUES.huge;
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

  // Consume small stones first
  while (remaining > 0 && newStones.small > 0) {
    newStones.small--;
    remaining -= MAGIC_STONE_VALUES.small;
  }

  // Then medium stones
  while (remaining > 0 && newStones.medium > 0) {
    newStones.medium--;
    remaining -= MAGIC_STONE_VALUES.medium;
  }

  // Then large stones
  while (remaining > 0 && newStones.large > 0) {
    newStones.large--;
    remaining -= MAGIC_STONE_VALUES.large;
  }

  // Finally huge stones
  while (remaining > 0 && newStones.huge > 0) {
    newStones.huge--;
    remaining -= MAGIC_STONE_VALUES.huge;
  }

  // Calculate actual value consumed (may be more than target due to stone denominations)
  const consumedValue = calculateMagicStoneTotal(stones) - calculateMagicStoneTotal(newStones);

  return {
    newStones,
    actualValue: consumedValue,
  };
}

/**
 * Purchase an item from a ShopListing
 * Creates an Item instance from ConsumableItemData
 */
export function purchaseItem(listing: ShopListing): Item | null {
  return generateConsumableFromData(listing.itemTypeId);
}

/**
 * Get the price for a ShopListing
 * Returns undefined if the listing cannot be resolved
 */
export function getListingPrice(listing: ShopListing): number | undefined {
  const resolved = resolveShopListing(listing);
  return resolved?.price;
}
