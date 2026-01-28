/**
 * Character Constants
 *
 * Centralized character-related constants including lives system,
 * class abilities, summon system, and elemental system.
 */

import type { Difficulty } from '@/types/characterTypes';
import type { ResonanceLevel, ElementType } from '@/types/characterTypes';
import type { DamageModifier } from "../domain/characters/classAbility/classAbilitySystem";
import type { ResonanceEffectConfig } from '@/types/characterTypes';
export type { ResonanceEffectConfig } from '@/types/characterTypes';

// ============================================================
// Lives System Constants
// ============================================================

/** Maximum lives by difficulty level */
export const LIVES_BY_DIFFICULTY: Record<Difficulty, number> = {
  easy: 3,
  normal: 3,
  hard: 2,
};

// ============================================================
// Swordsman Constants
// ============================================================

/** Maximum sword energy */
export const SWORD_ENERGY_MAX = 10;

// ============================================================
// Class Ability Constants
// ============================================================

/** Default damage modifier with no bonuses */
export const DEFAULT_DAMAGE_MODIFIER: DamageModifier = {
  flatBonus: 0,
  percentMultiplier: 1.0,
  critBonus: 0,
  penetration: 0,
};

// ============================================================
// Summoner Constants
// ============================================================

/** Maximum active summons */
export const MAX_ACTIVE_SUMMONS = 3;

/** Maximum bond level */
export const MAX_BOND_LEVEL = 10;

/** Bond level damage bonus per level (5% per level) */
export const BOND_DAMAGE_BONUS_PER_LEVEL = 0.05;

// ============================================================
// Mage / Elemental Constants
// ============================================================

/** Maximum resonance level */
export const MAX_RESONANCE_LEVEL: ResonanceLevel = 2;

/** Damage multiplier per resonance level */
export const RESONANCE_MULTIPLIER: Record<ResonanceLevel, number> = {
  0: 1.0,
  1: 1.15,  // +15%
  2: 1.30,  // +30%
};

/** Resonance effects table by element and level */
export const RESONANCE_EFFECTS: Record<ElementType, Record<1 | 2, ResonanceEffectConfig>> = {
  fire: {
    1: { burn: { stacks: 1, duration: 2 } },
    2: { burn: { stacks: 2, duration: 3 }, fieldBuff: "fireField" },
  },
  ice: {
    1: { freeze: { duration: 2 } },
    2: { freeze: { duration: 3 }, fieldBuff: "iceField" },
  },
  lightning: {
    1: {},
    2: { stun: { duration: 1 }, fieldBuff: "electroField" },
  },
  dark: {
    1: { lifesteal: 30 },
    2: { weakness: { duration: 3 }, lifesteal: 40, fieldBuff: "darkField" },
  },
  light: {
    1: { cleanse: 1 },
    2: { cleanse: 2, heal: 10, fieldBuff: "lightField" },
  },
};
