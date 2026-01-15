// Shop inventory data for the Merchant's Exchange

import type {
  ShopItem,
  EquipmentPackConfig,
  RarityProbability,
} from "../types/ShopTypes";

/**
 * Consumable items - Healing potions
 */
export const CONSUMABLE_ITEMS: ShopItem[] = [
  {
    id: "potion_small",
    name: "Small Healing Potion",
    description: "Recovers 30 HP. A basic remedy for minor wounds.",
    icon: "🧪",
    category: "consumable",
    price: 50,
    healAmount: 30,
  },
  {
    id: "potion_medium",
    name: "Medium Healing Potion",
    description: "Recovers 70 HP. A reliable restorative draught.",
    icon: "🧴",
    category: "consumable",
    price: 120,
    healAmount: 70,
  },
  {
    id: "potion_large",
    name: "Large Healing Potion",
    description: "Recovers 150 HP. A potent elixir brewed by master alchemists.",
    icon: "⚗️",
    category: "consumable",
    price: 240,
    healAmount: 150,
  },
];

/**
 * Teleport stones - Emergency escape items
 */
export const TELEPORT_ITEMS: ShopItem[] = [
  {
    id: "teleport_emergency",
    name: "Emergency Teleport Stone",
    description: "60% chance to return safely. Unreliable but cheap.",
    icon: "💎",
    category: "teleport",
    price: 100,
    returnChance: 0.6,
  },
  {
    id: "teleport_normal",
    name: "Teleport Stone",
    description: "70% chance to return safely. Standard issue for adventurers.",
    icon: "🔮",
    category: "teleport",
    price: 150,
    returnChance: 0.7,
  },
  {
    id: "teleport_blessed",
    name: "Blessed Teleport Stone",
    description: "80% chance to return safely. Imbued with divine protection.",
    icon: "✨",
    category: "teleport",
    price: 300,
    returnChance: 0.8,
  },
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
 * Get all shop items by category
 */
export function getShopItemsByCategory(
  category: "consumable" | "teleport"
): ShopItem[] {
  switch (category) {
    case "consumable":
      return CONSUMABLE_ITEMS;
    case "teleport":
      return TELEPORT_ITEMS;
    default:
      return [];
  }
}

/**
 * Get shop item by ID
 */
export function getShopItemById(id: string): ShopItem | undefined {
  return [...CONSUMABLE_ITEMS, ...TELEPORT_ITEMS].find((item) => item.id === id);
}

/**
 * Get equipment pack by ID
 */
export function getEquipmentPackById(id: string): EquipmentPackConfig | undefined {
  return EQUIPMENT_PACKS.find((pack) => pack.id === id);
}

/**
 * Get all purchasable items (for display)
 */
export function getAllShopItems(): {
  consumables: ShopItem[];
  teleport: ShopItem[];
  packs: EquipmentPackConfig[];
} {
  return {
    consumables: CONSUMABLE_ITEMS,
    teleport: TELEPORT_ITEMS,
    packs: EQUIPMENT_PACKS,
  };
}
