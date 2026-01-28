/**
 * Card Constants
 *
 * Centralized card-related constants including mastery thresholds,
 * magic multipliers, rarity colors, and category names.
 */

import type { CardCategory, MasteryLevel, Rarity } from '@/types/cardTypes';
import type { Depth } from '@/types/campTypes';

// ============================================================
// Magic & Mastery Constants
// ============================================================

/** Magic power multipliers by depth */
export const MAGIC_MULTIPLIERS: Record<Depth, number> = {
  1: 1,
  2: 2,
  3: 4,
  4: 8,
  5: 16,
};

/** Use count thresholds for mastery level progression */
export const MASTERY_THRESHOLDS = {
  0: 0,
  1: 8,
  2: 16,
  3: 24,
};

/** Damage bonus multipliers per mastery level */
export const MASTERY_BONUSES: Record<MasteryLevel, number> = {
  0: 1.0,
  1: 1.2,
  2: 1.4,
  3: 2.0,
};

// ============================================================
// Display Constants
// ============================================================

/** Card category display names */
export const CARD_CATEGORY_NAMES: Record<CardCategory, string> = {
  atk: "atk",
  def: "def",
  buff: "buff",
  debuff: "Debuff",
  heal: "Heal",
  swordEnergy: "Sword Energy",
};

/** Rarity display colors */
export const RARITY_COLORS: Record<Rarity, string> = {
  common: "#9ca3af",
  rare: "#3b82f6",
  epic: "#a855f7",
  legend: "#f59e0b",
};
