/**
 * Card Constants
 *
 * Centralized card-related constants including mastery thresholds,
 * magic multipliers, rarity colors, and category names.
 */

import type { CardCategory, MasteryLevel, Rarity } from '@/types/cardTypes';
import type { Depth } from '@/types/campTypes';
import type { ElementType } from '@/types/characterTypes';

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

// ============================================================
// Element Constants
// ============================================================

/** Element Unicode icons for UI display */
export const ELEMENT_ICON_MAP: Record<ElementType, string> = {
  slash: "⚔️",
  impact: "💥",
  guard: "🛡️",
  fire: "🔥",
  ice: "❄️",
  lightning: "⚡",
  dark: "🌑",
  light: "✨",
  summon: "🔮",
  enhance: "💎",
  sacrifice: "💀",
};

/** Element Japanese display names */
export const ELEMENT_LABEL_MAP: Record<ElementType, string> = {
  slash: "斬撃",
  impact: "衝撃",
  guard: "防御",
  fire: "火",
  ice: "氷",
  lightning: "雷",
  dark: "闇",
  light: "光",
  summon: "召喚",
  enhance: "強化",
  sacrifice: "生贄",
};

/** Element CSS colors */
export const ELEMENT_COLOR_MAP: Record<ElementType, string> = {
  slash: "#c0c0c0",
  impact: "#ff8c00",
  guard: "#4682b4",
  fire: "#ff4500",
  ice: "#00bfff",
  lightning: "#ffd700",
  dark: "#6a0dad",
  light: "#fffacd",
  summon: "#9370db",
  enhance: "#00ced1",
  sacrifice: "#8b0000",
};

/** Magic elements that can trigger Mage resonance chain */
export const MAGIC_ELEMENTS: ReadonlySet<ElementType> = new Set([
  "fire", "ice", "lightning", "dark", "light",
]);

/** Physical elements used by Swordsman class */
export const PHYSICAL_ELEMENTS: ReadonlySet<ElementType> = new Set([
  "slash", "impact", "guard",
]);

/** Summoner-specific elements */
export const SUMMONER_ELEMENTS: ReadonlySet<ElementType> = new Set([
  "summon", "enhance", "sacrifice",
]);
