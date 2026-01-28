// Shop inventory data for the Merchant's Exchange

import type { ShopItem } from "../../../domain/camps/types/ShopTypes";

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
 * Battle consumables - Items usable during combat
 */
export const BATTLE_ITEMS: ShopItem[] = [
  {
    id: "smoke_bomb",
    name: "Smoke Bomb",
    description: "Escape from a non-boss battle. Consumed on use.",
    icon: "💨",
    category: "battleItem",
    price: 80,
  },
  {
    id: "attack_powder",
    name: "Attack Powder",
    description: "Increases ATK by 20% for the current battle.",
    icon: "🔥",
    category: "battleItem",
    price: 120,
  },
  {
    id: "guard_charm",
    name: "Guard Charm",
    description: "Grants 10 Guard at the start of a battle.",
    icon: "🛡️",
    category: "battleItem",
    price: 100,
  },
];

/**
 * Map items - Items usable on the dungeon map
 */
export const MAP_ITEMS: ShopItem[] = [
  {
    id: "scout_lantern",
    name: "Scout Lantern",
    description: "Reveals the types of adjacent unrevealed nodes.",
    icon: "🔦",
    category: "mapItem",
    price: 60,
  },
  {
    id: "camp_kit",
    name: "Camp Kit",
    description: "Restore 50 HP at any map node. Single use.",
    icon: "⛺",
    category: "mapItem",
    price: 90,
    healAmount: 50,
  },
];

/**
 * Get all shop items by category
 */
export function getShopItemsByCategory(
  category: "consumable" | "teleport" | "battleItem" | "mapItem"
): ShopItem[] {
  switch (category) {
    case "consumable":
      return CONSUMABLE_ITEMS;
    case "teleport":
      return TELEPORT_ITEMS;
    case "battleItem":
      return BATTLE_ITEMS;
    case "mapItem":
      return MAP_ITEMS;
    default:
      return [];
  }
}

/**
 * Get shop item by ID
 */
export function getShopItemById(id: string): ShopItem | undefined {
  return [
    ...CONSUMABLE_ITEMS,
    ...TELEPORT_ITEMS,
    ...BATTLE_ITEMS,
    ...MAP_ITEMS,
  ].find((item) => item.id === id);
}

/**
 * Get all purchasable items (for display)
 */
export function getAllShopItems(): {
  consumables: ShopItem[];
  teleport: ShopItem[];
  battleItems: ShopItem[];
  mapItems: ShopItem[];
} {
  return {
    consumables: CONSUMABLE_ITEMS,
    teleport: TELEPORT_ITEMS,
    battleItems: BATTLE_ITEMS,
    mapItems: MAP_ITEMS,
  };
}
