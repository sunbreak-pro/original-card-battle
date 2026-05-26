// Dark Market constants - high-priced rare items updated on boss defeat

import type { Depth } from '@/types/cardTypes';
import type { ItemRarity } from '@/types/itemTypes';

/** Dark Market price multiplier (normal price multiplier) */
export const DARK_MARKET_PRICE_MULTIPLIER = 1.8;

/** Minimum equipment rarity for Dark Market (rare or higher only) */
export const DARK_MARKET_MIN_EQUIPMENT_RARITY: ItemRarity = 'rare';

/** Dark Market equipment slot count */
export const DARK_MARKET_EQUIPMENT_SLOTS = 4;

/** Legendary appearance rate by depth */
export const DARK_MARKET_LEGENDARY_RATE: Record<Depth, number> = {
  1: 0.05,
  2: 0.10,
  3: 0.15,
  4: 0.25,
  5: 0.35,
};

/** Dark Market exclusive consumable pool (epic or higher) */
export const DARK_MARKET_CONSUMABLE_POOL: { key: string; stock: number }[] = [
  { key: "time_stop_hourglass", stock: 1 },
  { key: "combo_elixir", stock: 1 },
  { key: "resurrection_stone", stock: 1 },
  { key: "greater_healing_potion", stock: 2 },
  { key: "full_elixir", stock: 1 },
];

/** Dark Market consumable slot count by depth (increases with depth) */
export const DARK_MARKET_CONSUMABLE_SLOTS_BY_DEPTH: Record<Depth, number> = {
  1: 2,
  2: 2,
  3: 3,
  4: 3,
  5: 4,
};
