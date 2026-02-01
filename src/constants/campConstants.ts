/**
 * Camp Constants
 *
 * Centralized camp facility constants including blacksmith upgrades,
 * sanctuary configuration, and shop pricing.
 */

import type { EquipmentQuality } from '@/types/itemTypes';
import type { SanctuaryEffects } from '@/types/campTypes';
import type { ElementType } from '@/types/characterTypes';
import type { GameScreen } from '@/types';

export const FACILITY_NAV_ITEMS: ReadonlyArray<{
  screen: GameScreen;
  label: string;
  icon: string;
}> = [
  { screen: "shop", label: "取引所", icon: "🏪" },
  { screen: "guild", label: "酒場", icon: "🍺" },
  { screen: "blacksmith", label: "鍛冶屋", icon: "⚒️" },
  { screen: "sanctuary", label: "聖域", icon: "⛪" },
  { screen: "library", label: "図書館", icon: "📚" },
  { screen: "storage", label: "倉庫", icon: "📦" },
] as const;
// ============================================================
// Blacksmith Constants
// ============================================================

/** Level modifiers for stats */
export const LEVEL_STAT_MODIFIERS: Record<number, number> = {
  0: 1.0,  // +0%
  1: 1.1,  // +10%
  2: 1.2,  // +20%
  3: 1.3,  // +30%
};

/** Level modifiers for AP (durability) */
export const LEVEL_AP_MODIFIERS: Record<number, number> = {
  0: 1.0,  // +0%
  1: 1.2,  // +20%
  2: 1.4,  // +40%
  3: 1.6,  // +60%
};

/** Quality modifiers for stats */
export const QUALITY_MODIFIERS: Record<EquipmentQuality, number> = {
  poor: 0.95,   // -5%
  normal: 1.0,  // +0%
  good: 1.03,   // +3%
  master: 1.05, // +5%
};

/** Quality display colors */
export const QUALITY_COLORS: Record<EquipmentQuality, string> = {
  poor: "#888888",   // Gray
  normal: "#ffffff", // White
  good: "#4ade80",   // Green
  master: "#fbbf24", // Gold
};

/** Quality display names (Japanese) */
export const QUALITY_NAMES: Record<EquipmentQuality, string> = {
  poor: "粗悪",
  normal: "通常",
  good: "良質",
  master: "傑作",
};

/** Quality order for progression */
export const QUALITY_ORDER: EquipmentQuality[] = ["poor", "normal", "good", "master"];

/** Maximum equipment level */
export const MAX_EQUIPMENT_LEVEL = 3;

// ============================================================
// Sanctuary Constants
// ============================================================

/** Default sanctuary effects (no nodes unlocked) */
export const DEFAULT_SANCTUARY_EFFECTS: SanctuaryEffects = {
  initialHpBonus: 0,
  goldMultiplier: 1.0,
  soulMultiplier: 1.0,
  explorationLimitBonus: 0,
  inventoryBonus: 0,
  hpRecoveryPercent: 0,
  hasAppraisal: false,
  hasTrueAppraisal: false,
  hasIndomitableWill: false,
  classEnergy: {
    swordsman: 0,
    mage: 0,
    summoner: 0,
  },
  enhancedElements: new Set<ElementType>(),
};

/** Sanctuary system constants */
export const SANCTUARY_CONSTANTS = {
  // Soul values
  SOUL_VALUES: {
    normal: 5,
    elite: 15,
    boss: 50,
    returnRouteMultiplier: 1.2,
  } as const,

  // Survival multipliers
  SURVIVAL_MULTIPLIERS: {
    earlyReturn: 0.6,
    normalReturn: 0.8,
    fullClear: 1.0,
  } as const,

  // UI constants
  UNLOCK_HOLD_DURATION: 1500, // 1.5 seconds for long press unlock
  TIER_RADIUS: {
    1: 1,
    2: 2,
    3: 3,
  } as const,
} as const;

// ============================================================
// Capacity Constants
// ============================================================

/** Storage maximum capacity */
export const STORAGE_MAX_CAPACITY = 100;

/** Inventory maximum capacity */
export const INVENTORY_MAX_CAPACITY = 20;

/** Equipment inventory maximum count */
export const EQUIPMENT_INVENTORY_MAX = 3;

/** Default exploration limit */
export const DEFAULT_EXPLORATION_LIMIT = 10;
