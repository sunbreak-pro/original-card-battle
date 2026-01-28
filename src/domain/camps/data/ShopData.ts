// Shop inventory data for the Merchant's Exchange

import type {
  ShopListing,
  EquipmentPackConfig,
  RarityProbability,
} from '@/types/campTypes';
import { getConsumableData } from "@/constants/data/items/ConsumableItemData";
import type { ConsumableItemData } from '@/types/itemTypes';

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
