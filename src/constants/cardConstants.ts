/**
 * Card Constants
 *
 * Centralized card-related constants including mastery thresholds,
 * magic multipliers, rarity colors, and category names.
 */

import type { MasteryLevel, CardTag } from '@/types/cardTypes';
import type { ElementType } from '@/types/characterTypes';

// ============================================================
// Magic & Mastery Constants
// ============================================================

/** Use count thresholds for mastery level progression */
export const MASTERY_THRESHOLDS = {
  0: 0,
  1: 8,
  2: 16,
  3: 24,
  4: 30,
};

/** Damage bonus multipliers per mastery level */
export const MASTERY_BONUSES: Record<MasteryLevel, number> = {
  0: 1.0,
  1: 1.2,
  2: 1.4,
  3: 2.0,
  4: 2.5,
};

// ============================================================
// Element Constants
// ============================================================

/** Magic elements that can trigger Mage resonance chain */
export const MAGIC_ELEMENTS: ReadonlySet<ElementType> = new Set([
  "fire", "ice", "lightning", "dark", "light",
]);

// ============================================================
// Card Tag Constants
// ============================================================

/** Card tag Japanese display names */
export const CARD_TAG_LABEL_MAP: Record<CardTag, string> = {
  attack: "攻撃",
  guard: "防御",
  skill: "スキル",
  stance: "構え",
};

/** Card tag CSS colors */
export const CARD_TAG_COLOR_MAP: Record<CardTag, string> = {
  attack: "#ef4444",
  guard: "#3b82f6",
  skill: "#8b5cf6",
  stance: "#f59e0b",
};
