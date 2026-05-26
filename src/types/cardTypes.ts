/**
 * Card Type Definitions
 *
 * Centralized card-related types including:
 * - Card interface with class-specific properties
 * - Mastery and gem level systems
 * - Depth type
 */

import type { CardBuffSpec } from './battleTypes';
import type { ElementType, CardCharacterClass } from './characterTypes';

// ============================================================
// Basic Types
// ============================================================

export type Depth = 1 | 2 | 3 | 4 | 5;
export type DepthCurveType = "shallow" | "neutral" | "deep" | "madness" | "adversity";

/**
 * Card Tag System
 * - attack: Cards that deal damage (baseDamage > 0)
 * - guard: Cards that provide defense (guardAmount > 0, no damage)
 * - skill: Temporary buffs/debuffs, utility cards (draw, energy, heal)
 * - stance: Semi-permanent battle effects (duration >= 99 or isPermanent: true)
 */
export type CardTag = "attack" | "guard" | "skill" | "stance";
export type MasteryLevel = 0 | 1 | 2 | 3 | 4;
export type GemLevel = 0 | 1 | 2;

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

  // ---- Cost ----
  /** Energy cost to play */
  cost: number;

  // ---- Mastery System ----
  /** Times this card has been used */
  useCount: number;
  /** Current mastery level (0-4) */
  masteryLevel: MasteryLevel;
  /** Gem enhancement level (0-2) */
  gemLevel: GemLevel;
  /** Progress toward next mastery level */
  talentProgress?: number;
  /** Threshold for next mastery level */
  talentThreshold?: number;

  // ---- Tags ----
  /** Tags for filtering/effects (attack, guard, skill, stance) */
  tags: CardTag[];

  // ---- Element ----
  /** Element types (every card has at least one, includes functional classification) */
  element: ElementType[];

  // ---- Common Effects ----
  /** Base damage dealt */
  baseDamage?: number;
  /** Effective power (after calculations) */
  effectivePower?: number;
  /** Number of hits (multi-hit attacks) */
  hitCount?: number;
  /** Target all enemies (AoE attack) */
  targetAll?: boolean;
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
  /** Convert sword energy to guard (multiplier applied to current energy) */
  convertEnergyToGuard?: { multiplier: number };

  // ---- Mage-Specific (Future) ----
  /** Bonus damage/effect for elemental chains */
  elementalChainBonus?: number;

  // ---- Card Derivation ----
  /** Card type ID this card was derived from (null = base card) */
  derivedFrom?: string | null;
  /** Mastery level required on parent card to unlock this card */
  unlockMasteryLevel?: MasteryLevel;
  /** Card type IDs this card can derive into */
  derivesInto?: string[];

  // ---- Talent Card System ----
  /** Whether this card is a talent card (unlocked via mastery of specific cards) */
  isTalentCard?: boolean;
}
