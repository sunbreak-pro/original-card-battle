/**
 * Item Type Definitions
 *
 * Centralized item-related types including:
 * - Item types and rarity
 * - Equipment types
 * - Consumable effect types
 * - Magic stones
 */

import type { BuffDebuffType } from './battleTypes';

// ============================================================
// Equipment Types
// ============================================================

export type EquipmentSlot =
  | "weapon"
  | "armor"
  | "helmet"
  | "boots"
  | "accessory1"
  | "accessory2";

export type EquipmentQuality = "poor" | "normal" | "good" | "master";

export interface EquipmentEffect {
  type: "stat" | "skill" | "passive";
  target: string;
  value: number | string;
  description: string;
}

export interface EquipmentData {
  id: string;
  typeId: string;
  name: string;
  description: string;
  itemType: "equipment";
  type: string;
  equipmentSlot: EquipmentSlot;
  durability: number;
  maxDurability: number;
  rarity: ItemRarity;
  quality: EquipmentQuality;
  effects: EquipmentEffect[];
}

/**
 * Equipment stat bonuses applied when equipped
 */
export interface EquipmentStatBonuses {
  hpBonus?: number;
  apBonus?: number;
  atkPercent?: number;
  defPercent?: number;
  speedBonus?: number;
  energyBonus?: number;
}

// ============================================================
// Item Types
// ============================================================

export type ItemType =
  | "equipment"
  | "consumable"
  | "magicStone"
  | "material"
  | "quest"
  | "key";

/**
 * Context where an item can be used
 */
export type UsableContext = 'battle' | 'map' | 'camp' | 'anywhere';

export type ItemRarity = "common" | "uncommon" | "rare" | "epic" | "legendary";

/**
 * Base item interface
 */
export interface Item {
  id: string;
  typeId: string;
  name: string;
  description: string;
  itemType: ItemType;
  type: string;

  // Equipment-specific properties
  equipmentSlot?: EquipmentSlot;
  durability?: number;
  maxDurability?: number;
  level?: number;
  quality?: EquipmentQuality;
  effects?: EquipmentEffect[];

  // Consumable-specific properties
  stackable?: boolean;
  stackCount?: number;
  maxStack?: number;
  usableContext?: UsableContext;

  // Magic Stone-specific properties
  magicStoneValue?: number;
  magicStoneSize?: "small" | "medium" | "large";

  // Common properties
  rarity: ItemRarity;
  sellPrice: number;
  canSell: boolean;
  canDiscard: boolean;
}

/**
 * Magic Stone currency structure
 */
export interface MagicStones {
  small: number;
  medium: number;
  large: number;
  huge: number;
}

// ============================================================
// Consumable Effect Types
// ============================================================

export type ConsumableEffectType =
  | 'heal'
  | 'fullHeal'
  | 'buff'
  | 'debuffClear'
  | 'damage'
  | 'shield'
  | 'energy'
  | 'draw'
  | 'skipEnemyTurn';

export interface ConsumableEffect {
  type: ConsumableEffectType;
  value?: number;
  buffType?: BuffDebuffType;
  duration?: number;
  targetAll?: boolean;
}

export interface ConsumableItemData {
  typeId: string;
  name: string;
  nameJa: string;
  description: string;
  descriptionJa: string;
  effects: ConsumableEffect[];
  usableContext: UsableContext;
  icon: string;
  rarity: 'common' | 'uncommon' | 'rare' | 'epic' | 'legendary';
  sellPrice: number;
  maxStack: number;
  shopPrice?: number;
}

export interface ItemEffectResult {
  success: boolean;
  hpChange?: number;
  guardChange?: number;
  energyChange?: number;
  cardsDrawn?: number;
  buffsApplied?: Array<{ type: BuffDebuffType; duration: number }>;
  debuffsCleared?: boolean;
  damageDealt?: number;
  skipEnemyTurn?: boolean;
  message: string;
}
