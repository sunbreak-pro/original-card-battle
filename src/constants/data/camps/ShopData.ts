// Shop inventory data for the Merchant's Exchange

import type {
  ShopListing,
} from '@/types/campTypes';
import type { EquipmentSlot, ItemRarity } from '@/types/itemTypes';
import { getConsumableData } from "@/constants/data/items/ConsumableItemData";
import type { ConsumableItemData } from '@/types/itemTypes';
import { EQUIPMENT_TEMPLATES } from "@/constants/data/items/EquipmentData";
import { EQUIPMENT_SLOTS, EQUIPMENT_BUY_PRICES } from "@/constants/itemConstants";
import { PERMANENT_SHOP_ITEMS } from './ShopStockConstants';

/**
 * Consumable listings - references ConsumableItemData by typeId
 * @deprecated Use PERMANENT_SHOP_ITEMS from ShopStockConstants instead for stock-aware listings
 */
export const CONSUMABLE_LISTINGS: ShopListing[] = [
  { itemTypeId: "healing_potion", category: "consumable" },
  { itemTypeId: "greater_healing_potion", category: "consumable" },
  { itemTypeId: "full_elixir", category: "consumable" },
];

/**
 * Teleport listings - references ConsumableItemData by typeId
 * @deprecated Teleport stones are now part of PERMANENT_SHOP_ITEMS
 */
export const TELEPORT_LISTINGS: ShopListing[] = [
  { itemTypeId: "teleport_stone", category: "teleport" },
];


/**
 * Resolve a ShopListing to its display info from ConsumableItemData
 */
export interface ResolvedShopListing {
  listing: ShopListing;
  data: ConsumableItemData;
  price: number;
}

/**
 * Get resolved listing with display data
 */
export function resolveShopListing(listing: ShopListing): ResolvedShopListing | null {
  const data = getConsumableData(listing.itemTypeId);
  if (!data || data.shopPrice === undefined) return null;
  return { listing, data, price: data.shopPrice };
}

/**
 * Resolve a consumable item key to its display data.
 * Used by the new stock-based shop system.
 */
export function resolveConsumableByKey(itemKey: string): ResolvedShopListing | null {
  const data = getConsumableData(itemKey);
  if (!data || data.shopPrice === undefined) return null;
  return {
    listing: { itemTypeId: itemKey, category: "consumable" },
    data,
    price: data.shopPrice,
  };
}

/**
 * Get all resolved permanent consumable listings (stock-aware system)
 */
export function getResolvedPermanentListings(): ResolvedShopListing[] {
  return PERMANENT_SHOP_ITEMS
    .map(item => resolveConsumableByKey(item.key))
    .filter((r): r is ResolvedShopListing => r !== null);
}

/**
 * Get resolved daily special listings for the given item keys
 */
export function getResolvedDailySpecialListings(keys: string[]): ResolvedShopListing[] {
  return keys
    .map(key => resolveConsumableByKey(key))
    .filter((r): r is ResolvedShopListing => r !== null);
}

/**
 * Get all resolved consumable listings
 * @deprecated Use getResolvedPermanentListings for the new system
 */
export function getResolvedConsumableListings(): ResolvedShopListing[] {
  return CONSUMABLE_LISTINGS
    .map(resolveShopListing)
    .filter((r): r is ResolvedShopListing => r !== null);
}

/**
 * Get all resolved teleport listings
 * @deprecated Teleport stones are now part of permanent shop items
 */
export function getResolvedTeleportListings(): ResolvedShopListing[] {
  return TELEPORT_LISTINGS
    .map(resolveShopListing)
    .filter((r): r is ResolvedShopListing => r !== null);
}

/**
 * Get shop listing by typeId
 */
export function getShopListingByTypeId(typeId: string): ShopListing | undefined {
  return [...CONSUMABLE_LISTINGS, ...TELEPORT_LISTINGS].find(
    (listing) => listing.itemTypeId === typeId
  );
}

// ============================================================
// Daily Equipment Inventory
// ============================================================

/**
 * Individual equipment listing for direct purchase
 */
export interface EquipmentListing {
  slot: EquipmentSlot;
  rarity: ItemRarity;
  name: string;
  icon: string;
  price: number;
}

/**
 * Simple seeded random number generator for deterministic daily rotation
 */
function seededRandom(seed: number): () => number {
  let s = seed;
  return () => {
    s = (s * 1664525 + 1013904223) & 0x7fffffff;
    return s / 0x7fffffff;
  };
}

/**
 * Generate deterministic daily equipment inventory
 * Shows 8 equipment pieces per day, rotating based on dayCount seed.
 * @param dayCount - The current day number (used as seed)
 * @returns Array of equipment listings available today
 */
export function generateDailyEquipmentInventory(dayCount: number): EquipmentListing[] {
  const rng = seededRandom(dayCount * 7919 + 31);
  const listings: EquipmentListing[] = [];

  // Pick 8 random slot+rarity combinations
  const numItems = 8;
  const usedSlots = new Set<string>();

  for (let i = 0; i < numItems; i++) {
    // Pick a slot (avoid duplicates within same day)
    let slotIdx = Math.floor(rng() * EQUIPMENT_SLOTS.length);
    let attempts = 0;
    while (usedSlots.has(EQUIPMENT_SLOTS[slotIdx]) && attempts < 10) {
      slotIdx = (slotIdx + 1) % EQUIPMENT_SLOTS.length;
      attempts++;
    }
    const slot = EQUIPMENT_SLOTS[slotIdx];
    usedSlots.add(slot);

    // Pick rarity (weighted toward lower rarities)
    const rarityRoll = rng();
    let rarity: ItemRarity;
    if (rarityRoll < 0.4) rarity = "common";
    else if (rarityRoll < 0.7) rarity = "uncommon";
    else if (rarityRoll < 0.9) rarity = "rare";
    else rarity = "epic";

    const template = EQUIPMENT_TEMPLATES[slot][rarity];
    listings.push({
      slot,
      rarity,
      name: template.name,
      icon: template.icon,
      price: EQUIPMENT_BUY_PRICES[rarity],
    });
  }

  return listings;
}
