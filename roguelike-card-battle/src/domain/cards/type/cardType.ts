/**
 * Card Type Definitions
 *
 * Defines all card-related types including:
 * - Card interface with class-specific properties
 * - Card categories and rarities
 * - Mastery and gem level systems
 */

import type { CardBuffSpec } from "../../battles/type/baffType";
import type { ElementType } from "../../characters/type/classAbilityTypes";

// ============================================================
// Basic Types
// ============================================================

export type Depth = 1 | 2 | 3 | 4 | 5;
export type CardCategory = "atk" | "def" | "buff" | "debuff" | "heal" | "swordEnergy";
export type DepthCurveType = "shallow" | "neutral" | "deep" | "madness" | "adversity";

/**
 * Card Tag System
 * - attack: Cards that deal damage (baseDamage > 0)
 * - guard: Cards that provide defense (guardAmount > 0 or category === "def", no damage)
 * - skill: Temporary buffs/debuffs, utility cards (draw, energy, heal)
 * - stance: Semi-permanent battle effects (duration >= 99 or isPermanent: true)
 */
export type CardTag = "attack" | "guard" | "skill" | "stance";
export type MasteryLevel = 0 | 1 | 2 | 3;
export type GemLevel = 0 | 1 | 2;
export type Rarity = "common" | "rare" | "epic" | "legend";

/**
 * Card character class assignment
 * - swordsman/mage/summoner: Class-specific cards
 * - common: Cards usable by all classes
 */
export type CardCharacterClass = "swordsman" | "mage" | "summoner" | "common";

// ============================================================
// Card Interface
// ============================================================

/**
 * Main Card Interface
 *
 * Represents a card in the game with all possible properties.
 * Some properties are class-specific (see comments below).
 */
export interface Card {
  // ---- Core Identity ----
  /** Unique instance ID */
  id: string;
  /** Card type ID (for same cards) */
  cardTypeId: string;
  /** Display name */
  name: string;
  /** Description text */
  description: string;

  // ---- Class Assignment (REQUIRED) ----
  /** Which class can use this card */
  characterClass: CardCharacterClass;

  // ---- Cost and Category ----
  /** Energy cost to play */
  cost: number;
  /** Card category (atk, def, buff, etc.) */
  category: CardCategory;
  /** Depth scaling curve type */
  /** Card rarity */
  rarity: Rarity;

  // ---- Mastery System ----
  /** Times this card has been used */
  useCount: number;
  /** Current mastery level (0-3) */
  masteryLevel: MasteryLevel;
  /** Gem enhancement level (0-2) */
  gemLevel: GemLevel;
  /** Progress toward next mastery level */
  talentProgress?: number;
  /** Threshold for next mastery level */
  talentThreshold?: number;

  // ---- Tags ----
  /** Tags for filtering/effects (attack, guard, skill, stance) */
  tags: CardTag;

  // ---- Common Effects ----
  /** Base damage dealt */
  baseDamage?: number;
  /** Effective power (after calculations) */
  effectivePower?: number;
  /** Number of hits (multi-hit attacks) */
  hitCount?: number;
  /** Penetration percentage (ignores AP) */
  penetration?: number;
  /** Acts before enemy turn */
  isPreemptive?: boolean;
  /** HP restored */
  healAmount?: number;
  /** Guard gained */
  guardAmount?: number;
  /** Cards drawn */
  drawCards?: number;
  /** Energy gained */
  energyGain?: number;
  /** Reduces next card cost */
  nextCardCostReduction?: number;
  /** Debuffs applied to enemy */
  applyEnemyDebuff?: CardBuffSpec[];
  /** Buffs applied to player */
  applyPlayerBuff?: CardBuffSpec[];

  // ---- Swordsman-Specific ----
  /** Sword energy gained when played */
  swordEnergyGain?: number;
  /** Sword energy consumed when played */
  swordEnergyConsume?: number;
  /** Damage multiplier per sword energy consumed */
  swordEnergyMultiplier?: number;

  // ---- Mage-Specific (Future) ----
  /** Element type for elemental chain */
  element?: ElementType;
  /** Bonus damage/effect for elemental chains */
  elementalChainBonus?: number;

  // ---- Summoner-Specific (Future) ----
  /** ID of summon to create/enhance */
  summonId?: string;
  /** Enhancement value for active summon */
  summonEnhancement?: number;
  /** Requires active summon to play */
  requiresSummon?: boolean;
}

// ============================================================
// Constants
// ============================================================

export const MAGIC_MULTIPLIERS: Record<Depth, number> = {
  1: 1,
  2: 2,
  3: 4,
  4: 8,
  5: 16,
};

export const MASTERY_THRESHOLDS = {
  0: 0,
  1: 8,
  2: 16,
  3: 24,
};

export const MASTERY_BONUSES: Record<MasteryLevel, number> = {
  0: 1.0,
  1: 1.2,
  2: 1.4,
  3: 2.0,
};

export const CARD_CATEGORY_NAMES: Record<CardCategory, string> = {
  atk: "atk",
  def: "def",
  buff: "buff",
  debuff: "Debuff",
  heal: "Heal",
  swordEnergy: "Sword Energy",
};

export const RARITY_COLORS: Record<Rarity, string> = {
  common: "#9ca3af",
  rare: "#3b82f6",
  epic: "#a855f7",
  legend: "#f59e0b",
};

// ============================================================
// Helper Functions
// ============================================================

/**
 * Check if card belongs to a specific class
 */
export function isCardForClass(card: Card, characterClass: "swordsman" | "mage" | "summoner"): boolean {
  return card.characterClass === characterClass || card.characterClass === "common";
}

/**
 * Check if card is class-specific (not common)
 */
export function isClassSpecificCard(card: Card): boolean {
  return card.characterClass !== "common";
}

/**
 * Get cards filtered by class
 */
export function filterCardsByClass(
  cards: Card[],
  characterClass: "swordsman" | "mage" | "summoner"
): Card[] {
  return cards.filter(card => isCardForClass(card, characterClass));
}
