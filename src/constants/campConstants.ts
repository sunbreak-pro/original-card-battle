/**
 * Camp Constants
 *
 * Centralized camp facility constants including blacksmith upgrades,
 * sanctuary configuration, and shop pricing.
 */

import type { EquipmentQuality } from '@/types/itemTypes';
import type { SanctuaryEffects } from '@/types/campTypes';
import type { ElementType } from '@/types/characterTypes';
import type { FacilityType, GameScreen } from '@/types';

/** Facility types that appear in navigation (excludes storage which is now in Guild) */
export type NavFacilityType = Exclude<FacilityType, 'storage'>;

export const FACILITY_NAV_ITEMS: ReadonlyArray<{
  screen: Exclude<GameScreen, 'storage'>;
  facilityType: NavFacilityType;
  label: string;
  icon: string;
  description: string;
  showInNav: boolean;
}> = [
  { screen: "dungeon", facilityType: "dungeon", label: "ダンジョンゲート", icon: "🌀", description: "Descend into the depths and face your destiny", showInNav: false },
  { screen: "shop", facilityType: "shop", label: "取引所", icon: "🏪", description: "Buy and sell cards, items, and relics", showInNav: true },
  { screen: "guild", facilityType: "guild", label: "酒場", icon: "🍺", description: "Rest, recruit companions, and hear rumors", showInNav: true },
  { screen: "blacksmith", facilityType: "blacksmith", label: "鍛冶屋", icon: "⚒️", description: "Forge and upgrade your equipment", showInNav: true },
  { screen: "sanctuary", facilityType: "sanctuary", label: "聖域", icon: "⛪", description: "Strengthen your soul with permanent upgrades", showInNav: true },
] as const;

// ============================================================
// Facility Tab Constants
// ============================================================

export const SHOP_TABS = [
  { id: "buy", label: "購入", icon: "🛒" },
  { id: "sell", label: "売却", icon: "💵" },
  { id: "exchange", label: "交換", icon: "🔄" },
  { id: "dark_market", label: "闇市場", icon: "🌙" },
] as const;

export const BLACKSMITH_TABS = [
  { id: "upgrade", label: "強化", icon: "⚒️" },
  { id: "repair", label: "修理", icon: "🔧" },
  { id: "dismantle", label: "分解", icon: "💎" },
] as const;

export const GUILD_TABS = [
  { id: "promotion", label: "昇格試験", icon: "⚔️" },
  { id: "rumors", label: "噂", icon: "👂" },
  { id: "quests", label: "依頼", icon: "📜" },
  { id: "storage", label: "倉庫", icon: "📦" },
] as const;

export const PREPARATION_TABS = [
  { id: "deck", label: "デッキ" },
  { id: "inventory", label: "持ち物" },
  { id: "equipment", label: "装備" },
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

// ============================================================
// Isometric Layout Constants
// ============================================================

/** Facility positions for isometric layout (clockwise from top) */
export type IsometricPosition = 'north' | 'northeast' | 'east' | 'southeast' | 'south' | 'southwest' | 'west' | 'northwest';

export const FACILITY_ISOMETRIC_POSITIONS: Record<NavFacilityType, IsometricPosition> = {
  dungeon: 'north',
  shop: 'northeast',
  guild: 'east',
  blacksmith: 'south',
  sanctuary: 'southwest',
};
