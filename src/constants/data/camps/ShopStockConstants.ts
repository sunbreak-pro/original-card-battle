// Shop stock system constants

import type { Depth } from '@/types/cardTypes';

/** Battle-count range for triggering a restock */
export const RESTOCK_BATTLE_RANGE = { min: 7, max: 10 } as const;

/** Epic item appearance rate by dungeon depth */
export const EPIC_RATE_BY_DEPTH: Record<Depth, number> = {
  1: 0.05,
  2: 0.07,
  3: 0.10,
  4: 0.15,
  5: 0.20,
};

/** Number of daily special consumable slots to fill */
export const DAILY_SPECIAL_SLOTS = 3;

/**
 * Permanent consumable listings — always available, restocked on restock.
 * Key matches the CONSUMABLE_ITEMS object key in ConsumableItemData.ts.
 */
export const PERMANENT_SHOP_ITEMS: { key: string; maxStock: number }[] = [
  { key: "healing_small_potion", maxStock: 5 },
  { key: "healing_potion", maxStock: 3 },
  { key: "greater_healing_potion", maxStock: 2 },
  { key: "full_elixir", maxStock: 1 },
  { key: "repair_kit", maxStock: 5 },
  { key: "teleport_stone", maxStock: 2 },
  { key: "antidote", maxStock: 3 },
  { key: "blessed_teleport_stone", maxStock: 2 },
  { key: "exp_boost", maxStock: 3 },
];

/** Weight tiers for daily special selection */
export type SpecialWeight = "high" | "medium" | "low";

const WEIGHT_VALUES: Record<SpecialWeight, number> = {
  high: 3,
  medium: 2,
  low: 1,
};

/**
 * Daily special consumable pool — randomly selected on each restock.
 * Key matches the CONSUMABLE_ITEMS object key in ConsumableItemData.ts.
 */
export const DAILY_SPECIAL_POOL: { key: string; stock: number; weight: SpecialWeight }[] = [
  { key: "energy_potion", stock: 3, weight: "high" },
  { key: "shield_potion", stock: 3, weight: "high" },
  { key: "haste_potion", stock: 2, weight: "medium" },
  { key: "draw_scroll", stock: 2, weight: "medium" },
  { key: "strength_elixir", stock: 1, weight: "low" },
  { key: "iron_skin_elixir", stock: 1, weight: "low" },
  { key: "magic_burst_crystal", stock: 1, weight: "low" },
  { key: "critical_elixir", stock: 2, weight: "medium" },
  { key: "emergency_teleport_stone", stock: 1, weight: "low" },
];

/**
 * Epic consumable pool — one may appear at depth-scaled probability.
 */
export const EPIC_CONSUMABLE_POOL: { key: string; stock: number }[] = [
  { key: "time_stop_hourglass", stock: 1 },
  { key: "combo_elixir", stock: 1 },
  { key: "resurrection_stone", stock: 1 },
];

/**
 * Get the numeric weight value for a SpecialWeight tier
 */
export function getWeightValue(weight: SpecialWeight): number {
  return WEIGHT_VALUES[weight];
}
