// Shop inventory data for the Merchant's Exchange

import type {
  ShopListing,
  EquipmentPackConfig,
  RarityProbability,
} from '@/types/campTypes';
import type { EquipmentSlot, ItemRarity } from '@/types/itemTypes';
import { getConsumableData } from "@/constants/data/items/ConsumableItemData";
import type { ConsumableItemData } from '@/types/itemTypes';
import { EQUIPMENT_TEMPLATES } from "@/constants/data/items/EquipmentData";
import { EQUIPMENT_SLOTS, EQUIPMENT_BUY_PRICES } from "@/constants/itemConstants";

/**
 * Consumable listings - references ConsumableItemData by typeId
 */
export const CONSUMABLE_LISTINGS: ShopListing[] = [
  { itemTypeId: "healing_potion", category: "consumable" },
  { itemTypeId: "greater_healing_potion", category: "consumable" },
  { itemTypeId: "full_elixir", category: "consumable" },
];

/**
 * Teleport listings - references ConsumableItemData by typeId
 */
export const TELEPORT_LISTINGS: ShopListing[] = [
  { itemTypeId: "teleport_stone", category: "teleport" },
];

/**
 * Rarity probability distributions for equipment packs
 */
const COMMON_PACK_PROBABILITIES: RarityProbability = {
  common: 1.0,
  uncommon: 0,
  rare: 0,
  epic: 0,
  legendary: 0,
};

const RARE_PACK_PROBABILITIES: RarityProbability = {
  common: 0.6,
  uncommon: 0,
  rare: 0.35,
  epic: 0.05,
  legendary: 0,
};

const EPIC_PACK_PROBABILITIES: RarityProbability = {
  common: 0.3,
  uncommon: 0,
  rare: 0.45,
  epic: 0.2,
  legendary: 0.05,
};

/**
 * Equipment packs - Gacha system
 */
export const EQUIPMENT_PACKS: EquipmentPackConfig[] = [
  {
    id: "pack_common",
    name: "Common Equipment Pack",
    description: "Contains 6 common equipment pieces. One for each slot.",
    icon: "📦",
    price: 300,
    packType: "common",
    guaranteedRarity: "common",
    itemCount: 6,
    rarityProbabilities: COMMON_PACK_PROBABILITIES,
  },
  {
    id: "pack_rare",
    name: "Rare Equipment Pack",
    description:
      "Contains 6 equipment pieces. Guaranteed Rare or better quality.",
    icon: "🎁",
    price: 500,
    packType: "rare",
    guaranteedRarity: "rare",
    itemCount: 6,
    rarityProbabilities: RARE_PACK_PROBABILITIES,
  },
  {
    id: "pack_epic",
    name: "Epic Equipment Pack",
    description:
      "Contains 6 equipment pieces. Guaranteed Epic or better quality. May contain Legendary items!",
    icon: "🏆",
    price: 1000,
    packType: "epic",
    guaranteedRarity: "epic",
    itemCount: 6,
    rarityProbabilities: EPIC_PACK_PROBABILITIES,
  },
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
 * Get all resolved consumable listings
 */
export function getResolvedConsumableListings(): ResolvedShopListing[] {
  return CONSUMABLE_LISTINGS
    .map(resolveShopListing)
    .filter((r): r is ResolvedShopListing => r !== null);
}

/**
 * Get all resolved teleport listings
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

/**
 * Get equipment pack by ID
 */
export function getEquipmentPackById(id: string): EquipmentPackConfig | undefined {
  return EQUIPMENT_PACKS.find((pack) => pack.id === id);
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

/** Available rarities for daily shop rotation */
const SHOP_RARITIES: ItemRarity[] = ["common", "uncommon", "rare", "epic"];

/**
 * Generate deterministic daily equipment inventory
 * Shows 4 equipment pieces per day, rotating based on dayCount seed.
 * @param dayCount - The current day number (used as seed)
 * @returns Array of equipment listings available today
 */
export function generateDailyEquipmentInventory(dayCount: number): EquipmentListing[] {
  const rng = seededRandom(dayCount * 7919 + 31);
  const listings: EquipmentListing[] = [];

  // Pick 4 random slot+rarity combinations
  const numItems = 4;
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

/**
 * Get all available equipment for direct purchase (non-rotating, full list)
 */
export function getAllEquipmentListings(): EquipmentListing[] {
  const listings: EquipmentListing[] = [];
  for (const slot of EQUIPMENT_SLOTS) {
    for (const rarity of SHOP_RARITIES) {
      const template = EQUIPMENT_TEMPLATES[slot][rarity];
      listings.push({
        slot,
        rarity,
        name: template.name,
        icon: template.icon,
        price: EQUIPMENT_BUY_PRICES[rarity],
      });
    }
  }
  return listings;
}
