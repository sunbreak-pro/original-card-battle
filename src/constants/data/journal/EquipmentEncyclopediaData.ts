/**
 * Equipment Encyclopedia Data for Journal
 *
 * Provides all equipment for the encyclopedia display.
 * Includes Swordsman, Mage, and Common equipment.
 */

import {
  SWORDSMAN_EQUIPMENT,
  SWORDSMAN_OFFHAND,
  SWORDSMAN_BOOTS,
  SWORDSMAN_ACCESSORIES,
  MAGE_EQUIPMENT,
  MAGE_OFFHAND,
  MAGE_BOOTS,
  MAGE_ACCESSORIES,
  COMMON_HEAD_EQUIPMENT,
  COMMON_BODY_EQUIPMENT,
  COMMON_WEAPON_EQUIPMENT,
  COMMON_OFFHAND_EQUIPMENT,
  COMMON_BOOTS_EQUIPMENT,
  COMMON_ACCESSORY_EQUIPMENT,
} from "@/constants/data/items/EquipmentData";
import type { EnhancedEquipmentData, ItemRarity } from "@/types/itemTypes";
import type { CharacterClass } from "@/types/characterTypes";

/**
 * Equipment encyclopedia entry
 */
export interface EquipmentEncyclopediaEntry {
  equipment: EnhancedEquipmentData;
  isDiscovered: boolean;
  timesEquipped: number;
  firstObtainedDate?: string;
}

/**
 * Equipment filter options
 */
export interface EquipmentFilterOptions {
  slot: string | null;
  rarity: ItemRarity | null;
  classRestriction: CharacterClass | "common" | null;
  searchText: string;
}

/**
 * Get all equipment from all categories
 */
export function getAllEquipment(): EnhancedEquipmentData[] {
  const allEquipment = [
    ...Object.values(SWORDSMAN_EQUIPMENT),
    ...Object.values(SWORDSMAN_OFFHAND),
    ...Object.values(SWORDSMAN_BOOTS),
    ...Object.values(SWORDSMAN_ACCESSORIES),
    ...Object.values(MAGE_EQUIPMENT),
    ...Object.values(MAGE_OFFHAND),
    ...Object.values(MAGE_BOOTS),
    ...Object.values(MAGE_ACCESSORIES),
    ...Object.values(COMMON_HEAD_EQUIPMENT),
    ...Object.values(COMMON_BODY_EQUIPMENT),
    ...Object.values(COMMON_WEAPON_EQUIPMENT),
    ...Object.values(COMMON_OFFHAND_EQUIPMENT),
    ...Object.values(COMMON_BOOTS_EQUIPMENT),
    ...Object.values(COMMON_ACCESSORY_EQUIPMENT),
  ];

  // Sort by rarity (legendary > epic > rare > uncommon > common), then by name
  const rarityOrder: Record<string, number> = {
    legendary: 5,
    epic: 4,
    rare: 3,
    uncommon: 2,
    common: 1,
  };

  return allEquipment.sort((a, b) => {
    const rarityDiff = (rarityOrder[b.rarity] || 0) - (rarityOrder[a.rarity] || 0);
    if (rarityDiff !== 0) return rarityDiff;
    return a.name.localeCompare(b.name);
  });
}

/**
 * Get equipment by class restriction
 */
export function getEquipmentByClass(
  classRestriction: CharacterClass | "common" | null
): EnhancedEquipmentData[] {
  const all = getAllEquipment();

  if (classRestriction === null) {
    return all;
  }

  if (classRestriction === "common") {
    return all.filter((eq) => !eq.classRestriction);
  }

  return all.filter((eq) => eq.classRestriction === classRestriction);
}

/**
 * Get equipment by slot
 */
export function getEquipmentBySlot(slot: string | null): EnhancedEquipmentData[] {
  const all = getAllEquipment();

  if (slot === null) {
    return all;
  }

  return all.filter((eq) => eq.equipmentSlot === slot || eq.type === slot);
}

/**
 * Create encyclopedia entries from equipment with discovery awareness
 */
export function createEquipmentEncyclopediaEntries(
  discoveredIds?: Set<string>
): EquipmentEncyclopediaEntry[] {
  const equipment = getAllEquipment();

  return equipment.map((eq) => ({
    equipment: eq,
    isDiscovered: !discoveredIds || discoveredIds.has(eq.id),
    timesEquipped: 0,
    firstObtainedDate: undefined,
  }));
}

/**
 * Get equipment statistics
 */
export function getEquipmentStats(discoveredIds?: Set<string>): {
  total: number;
  discovered: number;
  byRarity: Record<string, number>;
  bySlot: Record<string, number>;
  byClass: Record<string, number>;
} {
  const equipment = getAllEquipment();

  const byRarity: Record<string, number> = {};
  const bySlot: Record<string, number> = {};
  const byClass: Record<string, number> = {};
  let discovered = 0;

  equipment.forEach((eq) => {
    if (!discoveredIds || discoveredIds.has(eq.id)) {
      discovered++;
    }

    byRarity[eq.rarity] = (byRarity[eq.rarity] || 0) + 1;
    bySlot[eq.equipmentSlot] = (bySlot[eq.equipmentSlot] || 0) + 1;

    const classKey = eq.classRestriction || "common";
    byClass[classKey] = (byClass[classKey] || 0) + 1;
  });

  return {
    total: equipment.length,
    discovered,
    byRarity,
    bySlot,
    byClass,
  };
}

/**
 * Search equipment by name or description
 */
export function searchEquipment(searchText: string): EnhancedEquipmentData[] {
  const equipment = getAllEquipment();
  const lowerSearch = searchText.toLowerCase();

  if (!searchText) {
    return equipment;
  }

  return equipment.filter(
    (eq) =>
      eq.name.toLowerCase().includes(lowerSearch) ||
      eq.description.toLowerCase().includes(lowerSearch)
  );
}

/**
 * Equipment slot display names (Japanese)
 */
export const SLOT_NAMES: Record<string, string> = {
  weapon: "武器",
  armor: "鎧",
  helmet: "兜",
  boots: "靴",
  accessory1: "装飾品1",
  accessory2: "装飾品2",
  offhand: "左手",
};

/**
 * Equipment rarity display names (Japanese)
 */
export const RARITY_NAMES: Record<string, string> = {
  common: "コモン",
  uncommon: "アンコモン",
  rare: "レア",
  epic: "エピック",
  legendary: "レジェンダリー",
};

/**
 * Class display names (Japanese)
 */
export const CLASS_NAMES: Record<string, string> = {
  swordsman: "剣士",
  mage: "魔術師",
  common: "共用",
};
