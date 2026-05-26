// Rumor definitions for the Guild facility
// Temporary buffs purchased with magic stones that affect exploration

import type { Rumor } from "@/types/campTypes";

/**
 * All available rumors that can be purchased at the Guild
 * Cost is in magic stone value (small=30, medium=100, large=350, huge=1000)
 */
export const RUMORS: Rumor[] = [
  {
    id: "rumor_elite_hunter",
    name: "Elite Hunter's Tip",
    description:
      "Adventurers have spotted more powerful foes in the depths. Elite encounter rate increased by 20%.",
    cost: 100,
    effect: { type: "elite_rate", value: 0.2 },
    rarity: "common",
    icon: "💀",
    duration: 3,
  },
  {
    id: "rumor_merchant_favor",
    name: "Merchant's Favor",
    description:
      "A traveling merchant owes the Guild a debt. All shop purchases are 15% off.",
    cost: 150,
    effect: { type: "shop_discount", value: 0.15 },
    rarity: "common",
    icon: "💰",
    duration: 3,
  },
  {
    id: "rumor_treasure_map",
    name: "Tattered Treasure Map",
    description:
      "A faded map hints at hidden caches. Treasure room appearance rate increased by 25%.",
    cost: 200,
    effect: { type: "treasure_rate", value: 0.25 },
    rarity: "rare",
    icon: "🗺️",
    duration: 3,
  },
  {
    id: "rumor_potion_stash",
    name: "Abandoned Potion Stash",
    description:
      "Someone left potions near the dungeon entrance. Start exploration with 3 Small Healing Potions.",
    cost: 100,
    effect: { type: "start_bonus", bonus: "potion_small_x3" },
    rarity: "common",
    icon: "🧪",
    duration: 1,
  },
  {
    id: "rumor_elite_bounty",
    name: "Elite Bounty Notice",
    description:
      "The Guild is paying extra for elite scalps. Elite encounter rate increased by 40%.",
    cost: 300,
    effect: { type: "elite_rate", value: 0.4 },
    rarity: "rare",
    icon: "📜",
    duration: 3,
  },
  {
    id: "rumor_black_market",
    name: "Black Market Contact",
    description:
      "A shady figure offers deep discounts. All shop purchases are 25% off.",
    cost: 350,
    effect: { type: "shop_discount", value: 0.25 },
    rarity: "epic",
    icon: "🎭",
    duration: 5,
  },
  {
    id: "rumor_golden_path",
    name: "The Golden Path",
    description:
      "Legends speak of a golden trail. Treasure room appearance rate increased by 50%.",
    cost: 500,
    effect: { type: "treasure_rate", value: 0.5 },
    rarity: "epic",
    icon: "✨",
    duration: 3,
  },
  {
    id: "rumor_adventurer_cache",
    name: "Fallen Adventurer's Cache",
    description:
      "A recently deceased adventurer left supplies. Start with a Medium Healing Potion and Teleport Stone.",
    cost: 200,
    effect: { type: "start_bonus", bonus: "potion_medium_x1_teleport_normal_x1" },
    rarity: "rare",
    icon: "🎒",
    duration: 1,
  },
];

/**
 * Get rumor by ID
 */
export function getRumorById(id: string): Rumor | undefined {
  return RUMORS.find((r) => r.id === id);
}

/**
 * Get rumors filtered by rarity
 */
export function getRumorsByRarity(
  rarity: "common" | "rare" | "epic"
): Rumor[] {
  return RUMORS.filter((r) => r.rarity === rarity);
}
