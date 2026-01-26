// Blacksmith facility type definitions
import type { Item } from "../../item_equipment/type/ItemTypes";
import type { EquipmentQuality } from "../../item_equipment/type/EquipmentType";

/**
 * Blacksmith tab types
 */
export type BlacksmithTab = "upgrade" | "repair" | "dismantle";

/**
 * Quality upgrade option types
 * - normal: Standard cost, lower success rate
 * - qualityFocused: 1.5x cost, higher success rate
 * - maxQuality: 2x cost, highest success rate
 */
export type QualityUpOption = "normal" | "qualityFocused" | "maxQuality";

/**
 * Cost structure for upgrades
 */
export interface UpgradeCost {
  gold: number;
  magicStones: number; // Magic stone value (gold equivalent)
}

/**
 * Quality upgrade configuration
 */
export interface QualityUpConfig {
  option: QualityUpOption;
  label: string;
  description: string;
  costMultiplier: number; // 1.0 | 1.5 | 2.0
  successRates: {
    poorToNormal: number;
    normalToGood: number;
    goodToMaster: number;
  };
}

/**
 * Repair cost calculation result
 */
export interface RepairCost {
  gold: number;
  durabilityRestored: number;
}

/**
 * Dismantle result preview
 */
export interface DismantleResult {
  goldReturn: number;
  magicStoneChance: number;
  magicStoneReturn: number;
  bonusDescription: string;
}

/**
 * Blacksmith operation result
 */
export interface BlacksmithResult {
  success: boolean;
  message: string;
  item?: Item;
  goldChange?: number;
  magicStoneChange?: number;
  qualityChanged?: boolean;
}

/**
 * Upgrade preview for UI display
 */
export interface UpgradePreview {
  currentLevel: number;
  nextLevel: number;
  statChanges: {
    stat: string;
    before: number;
    after: number;
  }[];
  cost: UpgradeCost;
  canAfford: boolean;
}

/**
 * Quality upgrade preview for UI display
 */
export interface QualityUpgradePreview {
  currentQuality: EquipmentQuality;
  targetQuality: EquipmentQuality;
  successChance: number;
  cost: UpgradeCost;
  canAfford: boolean;
}

/**
 * Level modifiers for stats
 */
export const LEVEL_STAT_MODIFIERS: Record<number, number> = {
  0: 1.0,  // +0%
  1: 1.1,  // +10%
  2: 1.2,  // +20%
  3: 1.3,  // +30%
};

/**
 * Level modifiers for AP (durability)
 */
export const LEVEL_AP_MODIFIERS: Record<number, number> = {
  0: 1.0,  // +0%
  1: 1.2,  // +20%
  2: 1.4,  // +40%
  3: 1.6,  // +60%
};

/**
 * Quality modifiers for stats
 */
export const QUALITY_MODIFIERS: Record<EquipmentQuality, number> = {
  poor: 0.95,   // -5%
  normal: 1.0,  // +0%
  good: 1.03,   // +3%
  master: 1.05, // +5%
};

/**
 * Quality display colors
 */
export const QUALITY_COLORS: Record<EquipmentQuality, string> = {
  poor: "#888888",   // Gray
  normal: "#ffffff", // White
  good: "#4ade80",   // Green
  master: "#fbbf24", // Gold
};

/**
 * Quality display names (Japanese)
 */
export const QUALITY_NAMES: Record<EquipmentQuality, string> = {
  poor: "粗悪",
  normal: "通常",
  good: "良質",
  master: "傑作",
};

/**
 * Quality order for progression
 */
export const QUALITY_ORDER: EquipmentQuality[] = ["poor", "normal", "good", "master"];

/**
 * Maximum equipment level
 */
export const MAX_EQUIPMENT_LEVEL = 3;

/**
 * Helper: Get next quality tier
 */
export function getNextQuality(current: EquipmentQuality): EquipmentQuality | null {
  const index = QUALITY_ORDER.indexOf(current);
  if (index === -1 || index === QUALITY_ORDER.length - 1) return null;
  return QUALITY_ORDER[index + 1];
}

/**
 * Helper: Check if item is equipment
 */
export function isEquipment(item: Item): boolean {
  return item.itemType === "equipment";
}

/**
 * Helper: Check if equipment can be upgraded (level < max)
 */
export function canUpgradeLevel(item: Item): boolean {
  return isEquipment(item) && (item.level ?? 0) < MAX_EQUIPMENT_LEVEL;
}

/**
 * Helper: Check if equipment quality can be upgraded
 */
export function canUpgradeQuality(item: Item): boolean {
  return isEquipment(item) && item.quality !== undefined && item.quality !== "master";
}

/**
 * Helper: Check if equipment needs repair
 */
export function needsRepair(item: Item): boolean {
  return (
    isEquipment(item) &&
    item.durability !== undefined &&
    item.maxDurability !== undefined &&
    item.durability < item.maxDurability
  );
}

/**
 * Helper: Check if equipment can be dismantled
 */
export function canDismantle(item: Item): boolean {
  return isEquipment(item) && item.canDiscard;
}
