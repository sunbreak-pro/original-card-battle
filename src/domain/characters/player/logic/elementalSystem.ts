/**
 * Mage Class Ability: Elemental Resonance System
 *
 * [Elemental Resonance Mechanic]
 * Consecutive use of same-element cards builds resonance level (0-2).
 * Higher resonance provides damage bonuses and element-specific effects.
 *
 * [Resonance Effects]
 * - Level 0: Base damage only
 * - Level 1: Damage +15%, minor element-specific effect
 * - Level 2: Damage +30%, major effect + field buff
 *
 * [Element-Specific Effects]
 * - Fire: Burn stacks
 * - Ice: Freeze (action disable)
 * - Lightning: Stun at Lv2
 * - Dark: Lifesteal, weakness
 * - Light: Cleanse, heal
 */

import type { Card } from '@/types/cardTypes';
import type { ElementalState, ElementType, ResonanceLevel } from '@/types/characterTypes';
import { createInitialElemental } from '../../logic/classAbilityUtils';
import type { ClassAbilitySystem, DamageModifier } from "../../classAbility/classAbilitySystem";
import { DEFAULT_DAMAGE_MODIFIER } from "../../classAbility/classAbilitySystem";
import type { BuffDebuffType } from '@/types/battleTypes';
import { MAX_RESONANCE_LEVEL, RESONANCE_MULTIPLIER } from "../../../../constants";

// ============================================================
// Resonance Effects
// ============================================================

export interface ResonanceEffectConfig {
  /** Burn debuff config */
  burn?: { stacks: number; duration: number };
  /** Freeze debuff config */
  freeze?: { duration: number };
  /** Stun debuff config */
  stun?: { duration: number };
  /** Lifesteal percentage */
  lifesteal?: number;
  /** Number of debuffs to cleanse */
  cleanse?: number;
  /** Heal amount */
  heal?: number;
  /** Weakness debuff config */
  weakness?: { duration: number };
  /** Field buff to apply */
  fieldBuff?: BuffDebuffType;
}

/**
 * Resonance effects table by element and level
 */
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
    1: {},  // No effect at level 1
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
  slash: {
    1: {},
    2: {},
  },
  shock: {
    1: {},
    2: { stun: { duration: 1 } },
  },
  guard: {
    1: {},
    2: {},
  },
  summon: {
    1: {},
    2: {},
  },
  enhance: {
    1: {},
    2: {},
  },
  sacrifice: {
    1: {},
    2: {},
  },
};

// ============================================================
// Elemental System Implementation
// ============================================================

/**
 * Elemental Resonance System for Mage class
 */
export const ElementalSystem: ClassAbilitySystem<ElementalState> = {
  /**
   * Initialize elemental state
   */
  initialize(): ElementalState {
    return createInitialElemental();
  },

  /**
   * Handle card play - update resonance if elemental card
   */
  onCardPlay(state: ElementalState, card: Card): ElementalState {
    // Only process cards with element property
    if (!card.element) {
      // Non-elemental card breaks the resonance
      return {
        ...state,
        lastElement: null,
        resonanceLevel: 0,
      };
    }

    const isSameElement = state.lastElement === card.element;

    if (isSameElement) {
      // Continue resonance - increase level up to max
      const newLevel = Math.min(MAX_RESONANCE_LEVEL, state.resonanceLevel + 1) as ResonanceLevel;
      return {
        ...state,
        resonanceLevel: newLevel,
      };
    } else {
      // Different element - reset and start new resonance
      return {
        ...state,
        lastElement: card.element,
        resonanceLevel: 0,
      };
    }
  },

  /**
   * Handle turn start
   */
  onTurnStart(state: ElementalState): ElementalState {
    // No turn start effects
    return state;
  },

  /**
   * Handle turn end
   */
  onTurnEnd(state: ElementalState): ElementalState {
    // Resonance level resets at turn end
    return {
      ...state,
      resonanceLevel: 0,
    };
  },

  /**
   * Get damage modifier based on resonance level
   */
  getDamageModifier(state: ElementalState, card?: Card): DamageModifier {
    // Only apply bonus if playing an elemental card matching current element
    if (!card?.element || card.element !== state.lastElement) {
      return DEFAULT_DAMAGE_MODIFIER;
    }

    const multiplier = RESONANCE_MULTIPLIER[state.resonanceLevel];

    return {
      flatBonus: 0,
      percentMultiplier: multiplier,
      critBonus: state.resonanceLevel === 2 ? 0.10 : 0,
      penetration: 0,
    };
  },

  /**
   * Check if elemental action can be performed
   */
  canPerformAction(state: ElementalState, actionId: string): boolean {
    switch (actionId) {
      case "elemental_burst":
        // Can burst if at max resonance
        return state.resonanceLevel === MAX_RESONANCE_LEVEL;
      default:
        return true;
    }
  },

  /**
   * Get description of current elemental state
   */
  getStateDescription(state: ElementalState): string {
    if (state.resonanceLevel === 0 && !state.lastElement) {
      return "共鳴なし";
    }

    const elementNames: Record<ElementType, string> = {
      fire: "火",
      ice: "氷",
      lightning: "雷",
      dark: "闇",
      light: "光",
      slash: "斬",
      shock: "衝",
      guard: "盾",
      summon: "召",
      enhance: "強",
      sacrifice: "犠",
    };

    const elementName = state.lastElement ? elementNames[state.lastElement] : "";
    const resonanceNames = ["", "共鳴", "大共鳴"];
    return `${elementName}属性 ${resonanceNames[state.resonanceLevel]}`;
  },
};

// ============================================================
// Helper Functions
// ============================================================

/**
 * Get resonance effects for a given element and level
 */
export function getResonanceEffects(
  element: ElementType,
  level: ResonanceLevel
): ResonanceEffectConfig & { damageMultiplier: number } {
  const effects = level > 0 ? RESONANCE_EFFECTS[element][level as 1 | 2] : {};
  return {
    ...effects,
    damageMultiplier: RESONANCE_MULTIPLIER[level],
  };
}

/**
 * Update resonance state after playing a card
 */
export function updateResonance(
  state: ElementalState,
  cardElement: ElementType | undefined
): ElementalState {
  return ElementalSystem.onCardPlay(state, { element: cardElement } as Card);
}

/**
 * Reset resonance state
 */
export function resetResonance(state: ElementalState): ElementalState {
  return {
    ...state,
    lastElement: null,
    resonanceLevel: 0,
  };
}

/**
 * Check if element matches current resonance
 */
export function isMatchingElement(
  state: ElementalState,
  element: ElementType | undefined
): boolean {
  return element !== undefined && state.lastElement === element;
}
