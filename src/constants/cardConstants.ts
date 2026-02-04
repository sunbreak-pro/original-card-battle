/**
 * Card Constants
 *
 * Centralized card-related constants including mastery thresholds,
 * magic multipliers, rarity colors, and category names.
 */

import type { MasteryLevel, CardTag } from '@/types/cardTypes';
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
// Element Constants
// ============================================================

/** Element Japanese display names */
export const ELEMENT_LABEL_MAP: Record<ElementType, string> = {
  physics: "物理",
  guard: "防御",
  fire: "火",
  ice: "氷",
  lightning: "雷",
  dark: "闇",
  light: "光",
  buff: "バフ",
  debuff: "デバフ",
  heal: "回復",
  attack: "攻撃",
  classAbility: "固有技",
  chain: "連鎖",
};

/** Element CSS colors */
export const ELEMENT_COLOR_MAP: Record<ElementType, string> = {
  physics: "#c0c0c0",
  guard: "#4682b4",
  fire: "#ff4500",
  ice: "#00bfff",
  lightning: "#ffd700",
  dark: "#6a0dad",
  light: "#fffacd",
  buff: "#32cd32",
  debuff: "#dc143c",
  heal: "#00fa9a",
  attack: "#ff6b6b",
  classAbility: "#ffd93d",
  chain: "#6bcb77",
};

/** Magic elements that can trigger Mage resonance chain */
export const MAGIC_ELEMENTS: ReadonlySet<ElementType> = new Set([
  "fire", "ice", "lightning", "dark", "light",
]);

/** Physical elements used by Swordsman class */
export const PHYSICAL_ELEMENTS: ReadonlySet<ElementType> = new Set([
  "physics", "guard",
]);

/** Utility elements (non-combat element types) */
export const UTILITY_ELEMENTS: ReadonlySet<ElementType> = new Set([
  "buff", "debuff", "heal",
]);

/** Functional elements used for card effect classification */
export const FUNCTIONAL_ELEMENTS: ReadonlySet<ElementType> = new Set([
  "attack", "guard", "buff", "debuff", "heal",
]);

/** Class ability elements */
export const CLASS_ABILITY_ELEMENTS: ReadonlySet<ElementType> = new Set([
  "classAbility", "chain",
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
