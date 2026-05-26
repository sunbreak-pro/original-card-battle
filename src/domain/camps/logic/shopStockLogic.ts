// Shop stock management logic — handles initialization, purchase, restock

import type { ShopStockState } from '@/types/campTypes';
import type { Depth } from '@/types/cardTypes';
import {
  RESTOCK_BATTLE_RANGE,
  EPIC_RATE_BY_DEPTH,
  DAILY_SPECIAL_SLOTS,
  PERMANENT_SHOP_ITEMS,
  DAILY_SPECIAL_POOL,
  EPIC_CONSUMABLE_POOL,
  getWeightValue,
} from '@/constants/data/camps/ShopStockConstants';
import {
  DARK_MARKET_CONSUMABLE_POOL,
  DARK_MARKET_CONSUMABLE_SLOTS_BY_DEPTH,
} from '@/constants/data/camps/DarkMarketConstants';

// ============================================================
// Seeded RNG (same algorithm as ShopData.ts)
// ============================================================

function seededRandom(seed: number): () => number {
  let s = seed;
  return () => {
    s = (s * 1664525 + 1013904223) & 0x7fffffff;
    return s / 0x7fffffff;
  };
}

// ============================================================
// Restock Threshold
// ============================================================

/**
 * Generate a random restock threshold (7-10 battles)
 */
export function generateRestockThreshold(rng?: () => number): number {
  const range = RESTOCK_BATTLE_RANGE.max - RESTOCK_BATTLE_RANGE.min + 1;
  if (rng) {
    return RESTOCK_BATTLE_RANGE.min + Math.floor(rng() * range);
  }
  return RESTOCK_BATTLE_RANGE.min + Math.floor(Math.random() * range);
}

// ============================================================
// Daily Special Selection
// ============================================================

/**
 * Select daily special items from the pool using weighted random.
 * Also rolls for an epic item based on depth.
 */
function selectDailySpecials(
  rng: () => number,
  depth: Depth,
): { keys: string[]; stock: Record<string, number> } {
  const selected: string[] = [];
  const stock: Record<string, number> = {};
  const available = [...DAILY_SPECIAL_POOL];

  // Pick DAILY_SPECIAL_SLOTS items via weighted random without replacement
  for (let i = 0; i < DAILY_SPECIAL_SLOTS && available.length > 0; i++) {
    const currentTotalWeight = available.reduce(
      (sum, item) => sum + getWeightValue(item.weight),
      0,
    );
    let roll = rng() * currentTotalWeight;

    let pickedIdx = 0;
    for (let j = 0; j < available.length; j++) {
      roll -= getWeightValue(available[j].weight);
      if (roll <= 0) {
        pickedIdx = j;
        break;
      }
    }

    const picked = available[pickedIdx];
    selected.push(picked.key);
    stock[picked.key] = picked.stock;
    available.splice(pickedIdx, 1);
  }

  // Epic roll based on depth
  const epicRate = EPIC_RATE_BY_DEPTH[depth];
  if (rng() < epicRate && EPIC_CONSUMABLE_POOL.length > 0) {
    const epicIdx = Math.floor(rng() * EPIC_CONSUMABLE_POOL.length);
    const epic = EPIC_CONSUMABLE_POOL[epicIdx];
    selected.push(epic.key);
    stock[epic.key] = epic.stock;
  }

  return { keys: selected, stock };
}

// ============================================================
// Dark Market Stock Selection
// ============================================================

/**
 * Select dark market consumables from the pool based on depth.
 */
function selectDarkMarketConsumables(
  rng: () => number,
  depth: Depth,
): Record<string, number> {
  const stock: Record<string, number> = {};
  const available = [...DARK_MARKET_CONSUMABLE_POOL];
  const slotCount = DARK_MARKET_CONSUMABLE_SLOTS_BY_DEPTH[depth];

  for (let i = 0; i < slotCount && available.length > 0; i++) {
    const idx = Math.floor(rng() * available.length);
    const picked = available[idx];
    stock[picked.key] = picked.stock;
    available.splice(idx, 1);
  }

  return stock;
}

/**
 * Initialize dark market stock (called on first visit or boss defeat)
 */
export function initializeDarkMarketStock(
  seed: number,
  depth: Depth,
): Pick<ShopStockState,
  'darkMarketConsumableStock' |
  'darkMarketEquipmentSoldOutIndices' |
  'darkMarketSeed' |
  'darkMarketHasNewStock'
> {
  const rng = seededRandom(seed * 8831 + 17);
  const consumableStock = selectDarkMarketConsumables(rng, depth);

  return {
    darkMarketConsumableStock: consumableStock,
    darkMarketEquipmentSoldOutIndices: [],
    darkMarketSeed: seed,
    darkMarketHasNewStock: false,
  };
}

// ============================================================
// Initialization
// ============================================================

/**
 * Initialize a fresh shop stock state.
 * Called on first shop visit or after a forced restock.
 */
export function initializeShopStock(seed: number, depth: Depth): ShopStockState {
  const rng = seededRandom(seed * 7919 + 31);

  // Build permanent stock
  const permanentStock: Record<string, number> = {};
  for (const item of PERMANENT_SHOP_ITEMS) {
    permanentStock[item.key] = item.maxStock;
  }

  // Build daily specials
  const { keys: dailySpecialKeys, stock: dailySpecialStock } = selectDailySpecials(rng, depth);

  // Initialize dark market
  const darkMarketUpdate = initializeDarkMarketStock(seed, depth);

  return {
    permanentStock,
    dailySpecialStock,
    equipmentSoldOutIndices: [],
    dailySpecialKeys,
    battlesSinceRestock: 0,
    restockThreshold: generateRestockThreshold(rng),
    rotationSeed: seed,
    hasNewStock: false,
    // Dark Market fields
    ...darkMarketUpdate,
  };
}

// ============================================================
// Stock Operations
// ============================================================

/**
 * Decrement stock for a purchased consumable item.
 * Returns updated state, or null if item is out of stock.
 */
export function decrementConsumableStock(
  state: ShopStockState,
  itemKey: string,
): ShopStockState | null {
  // Check permanent stock
  if (itemKey in state.permanentStock) {
    const current = state.permanentStock[itemKey];
    if (current <= 0) return null;
    return {
      ...state,
      permanentStock: {
        ...state.permanentStock,
        [itemKey]: current - 1,
      },
    };
  }

  // Check daily special stock
  if (itemKey in state.dailySpecialStock) {
    const current = state.dailySpecialStock[itemKey];
    if (current <= 0) return null;
    return {
      ...state,
      dailySpecialStock: {
        ...state.dailySpecialStock,
        [itemKey]: current - 1,
      },
    };
  }

  return null;
}

/**
 * Mark an equipment index as sold out.
 * Returns updated state, or null if already sold out.
 */
export function markEquipmentSoldOut(
  state: ShopStockState,
  equipmentIndex: number,
): ShopStockState | null {
  if (state.equipmentSoldOutIndices.includes(equipmentIndex)) return null;
  return {
    ...state,
    equipmentSoldOutIndices: [...state.equipmentSoldOutIndices, equipmentIndex],
  };
}

/**
 * Check if a consumable item is in stock.
 */
export function isConsumableInStock(state: ShopStockState, itemKey: string): boolean {
  if (itemKey in state.permanentStock) {
    return state.permanentStock[itemKey] > 0;
  }
  if (itemKey in state.dailySpecialStock) {
    return state.dailySpecialStock[itemKey] > 0;
  }
  return false;
}

/**
 * Check if an equipment slot is still available.
 */
export function isEquipmentAvailable(state: ShopStockState, index: number): boolean {
  return !state.equipmentSoldOutIndices.includes(index);
}

/**
 * Get remaining stock count for a consumable.
 */
export function getConsumableStock(state: ShopStockState, itemKey: string): number {
  if (itemKey in state.permanentStock) {
    return state.permanentStock[itemKey];
  }
  if (itemKey in state.dailySpecialStock) {
    return state.dailySpecialStock[itemKey];
  }
  return 0;
}

// ============================================================
// Restock
// ============================================================

/**
 * Check if battle count has reached the restock threshold.
 */
export function shouldRestock(state: ShopStockState): boolean {
  return state.battlesSinceRestock >= state.restockThreshold;
}

/**
 * Increment battle counter after a battle.
 * Sets hasNewStock flag if threshold is reached.
 */
export function incrementBattleCount(state: ShopStockState): ShopStockState {
  const newCount = state.battlesSinceRestock + 1;
  const thresholdReached = newCount >= state.restockThreshold;
  return {
    ...state,
    battlesSinceRestock: newCount,
    hasNewStock: thresholdReached ? true : state.hasNewStock,
  };
}

/**
 * Perform a restock: regenerate daily specials, reset permanent stock,
 * clear equipment sold-out, reset battle counter, generate new threshold.
 * Dark market stock is preserved (only updated on boss defeat).
 */
export function performRestock(newSeed: number, depth: Depth, existingState?: ShopStockState): ShopStockState {
  const rng = seededRandom(newSeed * 7919 + 31);

  // Reset permanent stock to max
  const permanentStock: Record<string, number> = {};
  for (const item of PERMANENT_SHOP_ITEMS) {
    permanentStock[item.key] = item.maxStock;
  }

  // Generate new daily specials
  const { keys: dailySpecialKeys, stock: dailySpecialStock } = selectDailySpecials(rng, depth);

  // Preserve existing dark market state or initialize fresh
  const darkMarketFields = existingState
    ? {
        darkMarketConsumableStock: existingState.darkMarketConsumableStock,
        darkMarketEquipmentSoldOutIndices: existingState.darkMarketEquipmentSoldOutIndices,
        darkMarketSeed: existingState.darkMarketSeed,
        darkMarketHasNewStock: existingState.darkMarketHasNewStock,
      }
    : initializeDarkMarketStock(newSeed, depth);

  return {
    permanentStock,
    dailySpecialStock,
    equipmentSoldOutIndices: [],
    dailySpecialKeys,
    battlesSinceRestock: 0,
    restockThreshold: generateRestockThreshold(rng),
    rotationSeed: newSeed,
    hasNewStock: true,
    ...darkMarketFields,
  };
}

/**
 * Force restock via merchant ticket.
 * Generates a completely fresh stock using current time as seed.
 */
export function forceRestock(depth: Depth): ShopStockState {
  const newSeed = Math.floor(Date.now() / 1000);
  return initializeShopStock(newSeed, depth);
}

/**
 * Clear the "new stock available" notification flag.
 */
export function clearNewStockFlag(state: ShopStockState): ShopStockState {
  if (!state.hasNewStock) return state;
  return {
    ...state,
    hasNewStock: false,
  };
}

// ============================================================
// Dark Market Stock Operations
// ============================================================

/**
 * Refresh dark market on boss defeat.
 * Generates new stock and sets the newStock flag.
 */
export function refreshDarkMarketOnBossDefeat(
  state: ShopStockState,
  depth: Depth,
): ShopStockState {
  const newSeed = Math.floor(Date.now() / 1000);
  const darkMarketUpdate = initializeDarkMarketStock(newSeed, depth);
  return {
    ...state,
    ...darkMarketUpdate,
    darkMarketHasNewStock: true,
  };
}

/**
 * Decrement dark market consumable stock.
 * Returns updated state, or null if item is out of stock.
 */
export function decrementDarkMarketConsumableStock(
  state: ShopStockState,
  itemKey: string,
): ShopStockState | null {
  if (!(itemKey in state.darkMarketConsumableStock)) return null;
  const current = state.darkMarketConsumableStock[itemKey];
  if (current <= 0) return null;
  return {
    ...state,
    darkMarketConsumableStock: {
      ...state.darkMarketConsumableStock,
      [itemKey]: current - 1,
    },
  };
}

/**
 * Mark dark market equipment as sold out.
 * Returns updated state, or null if already sold out.
 */
export function markDarkMarketEquipmentSoldOut(
  state: ShopStockState,
  index: number,
): ShopStockState | null {
  if (state.darkMarketEquipmentSoldOutIndices.includes(index)) return null;
  return {
    ...state,
    darkMarketEquipmentSoldOutIndices: [...state.darkMarketEquipmentSoldOutIndices, index],
  };
}

/**
 * Clear dark market new stock flag.
 */
export function clearDarkMarketNewStockFlag(
  state: ShopStockState,
): ShopStockState {
  if (!state.darkMarketHasNewStock) return state;
  return {
    ...state,
    darkMarketHasNewStock: false,
  };
}

/**
 * Check if a dark market consumable is in stock.
 */
export function isDarkMarketConsumableInStock(
  state: ShopStockState,
  itemKey: string,
): boolean {
  if (!(itemKey in state.darkMarketConsumableStock)) return false;
  return state.darkMarketConsumableStock[itemKey] > 0;
}

/**
 * Get dark market consumable stock count.
 */
export function getDarkMarketConsumableStock(
  state: ShopStockState,
  itemKey: string,
): number {
  return state.darkMarketConsumableStock[itemKey] ?? 0;
}

/**
 * Check if dark market equipment is available.
 */
export function isDarkMarketEquipmentAvailable(
  state: ShopStockState,
  index: number,
): boolean {
  return !state.darkMarketEquipmentSoldOutIndices.includes(index);
}
