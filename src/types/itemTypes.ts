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
// Equipment Skill System
// ============================================================

import type { CharacterClass } from './characterTypes';

/** Equipment upgrade level (0 = base, 3 = max) */
export type EquipmentUpgradeLevel = 0 | 1 | 2 | 3;

/** Equipment skill effect types */
export type EquipmentSkillEffectType =
  | 'damageReduction'       // Reduce incoming damage by percentage
  | 'damageBonus'           // Increase outgoing damage by percentage
  | 'shieldOnTurnStart'     // Gain shield at turn start
  | 'counterAttack'         // Deal damage when hit
  | 'classAbilityBonus'     // Boost class-specific ability (sword energy, resonance, summon)
  | 'criticalBonus'         // Increase critical rate/damage
  | 'healingBonus'          // Increase healing received
  | 'energyBonus'           // Bonus energy per turn
  | 'drawBonus'             // Draw extra cards
  | 'penetrationBonus'      // Increase penetration
  | 'statusResist'          // Resist debuffs
  | 'lifesteal'             // Heal on dealing damage
  | 'bonusOnLowHp'          // Trigger effect when HP is low
  | 'bonusOnFullHp'         // Trigger effect when HP is full
  | 'bonusPerBuff'          // Scale effect with active buffs
  | 'bonusPerDebuff'        // Scale effect with enemy debuffs
  | 'bonusPerSwordEnergy'   // Swordsman: scale with sword energy
  | 'bonusPerResonance'     // Mage: scale with resonance level
  | 'bonusPerSummon';       // Summoner: scale with active summons

/**
 * Equipment skill effect definition
 */
export interface EquipmentSkillEffect {
  type: EquipmentSkillEffectType;
  /** Numeric value for the effect (percentage, flat value, etc.) */
  value: number;
  /** Optional condition for activation */
  condition?: string;
  /** Class ability target (for classAbilityBonus type) */
  classAbilityTarget?: 'swordEnergy' | 'resonance' | 'summon';
}

/**
 * Equipment skill definition
 */
export interface EquipmentSkill {
  id: string;
  name: string;
  nameJa: string;
  description: string;
  descriptionJa: string;
  effect: EquipmentSkillEffect;
  /** Upgrade level required to unlock (0 = initial skill, 3 = unlocked at max upgrade) */
  unlockLevel: 0 | 3;
}

/**
 * Enhanced equipment data with upgrade system
 */
export interface EnhancedEquipmentData extends EquipmentData {
  /** Current upgrade level (0-3) */
  upgradeLevel: EquipmentUpgradeLevel;
  /** Equipment skills (initial + potentially unlocked at level 3) */
  skills: EquipmentSkill[];
  /** Class restriction (if any) */
  classRestriction?: CharacterClass;
  /** Base AP value at upgrade level 0 */
  baseAp: number;
}

/**
 * Equipment upgrade cost definition
 */
export interface EquipmentUpgradeCost {
  gold: number;
  magicStones: number;
}

/**
 * Upgrade costs by rarity and target level
 */
export type UpgradeCostTable = Record<ItemRarity, Record<1 | 2 | 3, EquipmentUpgradeCost>>;

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
  | 'skipEnemyTurn'
  | 'resurrection'
  | 'return'
  | 'criticalBoost'
  | 'expBoost';

export interface ConsumableEffect {
  type: ConsumableEffectType;
  value?: number;
  buffType?: BuffDebuffType;
  duration?: number;
  targetAll?: boolean;
  /** Return mode: 'blessed' (80% rewards) | 'emergency' (60% rewards, usable in battle) */
  returnMode?: 'blessed' | 'emergency';
  /** HP recovery percentage for resurrection (e.g., 0.5 = 50%) */
  hpRecoveryPercent?: number;
  /** Percentage boost for experience/mastery gains */
  boostPercent?: number;
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
