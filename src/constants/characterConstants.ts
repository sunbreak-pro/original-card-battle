/**
 * Character Constants
 *
 * Centralized character-related constants including lives system,
 * class abilities, and elemental system.
 */

import type { Difficulty } from '@/types/characterTypes';
import type { ResonanceLevel, ElementType } from '@/types/characterTypes';
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
  physics: {
    1: {},
    2: {},
  },
  slash: {
    1: {},
    2: {},
  },
  impact: {
    1: {},
    2: {},
  },
  guard: {
    1: {},
    2: {},
  },
  buff: {
    1: {},
    2: {},
  },
  debuff: {
    1: {},
    2: {},
  },
  heal: {
    1: {},
    2: {},
  },
  attack: {
    1: {},
    2: {},
  },
  classAbility: {
    1: {},
    2: {},
  },
  chain: {
    1: {},
    2: {},
  },
};
