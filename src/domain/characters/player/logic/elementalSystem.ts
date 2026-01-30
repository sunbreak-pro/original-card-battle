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
import type { ElementalState, ElementType, ResonanceLevel, ResonanceEffectConfig } from '@/types/characterTypes';
import { createInitialElemental } from '../../logic/classAbilityUtils';
import type { ClassAbilitySystem, DamageModifier } from "../../classAbility/classAbilitySystem";
import { DEFAULT_DAMAGE_MODIFIER } from "../../classAbility/classAbilitySystem";
import { MAX_RESONANCE_LEVEL, RESONANCE_MULTIPLIER, RESONANCE_EFFECTS } from "../../../../constants";
import { MAGIC_ELEMENTS } from '@/constants/cardConstants';

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
    // Only magic elements can build/maintain resonance chain
    if (!card.element || !MAGIC_ELEMENTS.has(card.element)) {
      // Non-magic element card breaks the resonance
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
      impact: "衝",
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
 * Get resonance effects for a given element and level.
 * When enhancedElements is provided and contains the element,
 * the base resonance effects are strengthened.
 */
export function getResonanceEffects(
  element: ElementType,
  level: ResonanceLevel,
  enhancedElements?: ReadonlySet<ElementType>
): ResonanceEffectConfig & { damageMultiplier: number } {
  const baseEffects = level > 0 ? RESONANCE_EFFECTS[element][level as 1 | 2] : {};
  const result: ResonanceEffectConfig & { damageMultiplier: number } = {
    ...baseEffects,
    damageMultiplier: RESONANCE_MULTIPLIER[level],
  };

  // Apply sanctuary element enhancement bonuses
  if (enhancedElements?.has(element) && level > 0) {
    switch (element) {
      case "fire":
        if (result.burn) {
          result.burn = { ...result.burn, stacks: result.burn.stacks + 1 };
        }
        break;
      case "ice":
        if (result.freeze) {
          result.freeze = { ...result.freeze, duration: result.freeze.duration + 1 };
        }
        break;
      case "lightning":
        if (!result.stun) {
          result.stun = { duration: 1 };
        }
        break;
      case "dark":
        if (result.lifesteal !== undefined) {
          result.lifesteal = result.lifesteal + 10;
        }
        break;
      case "light":
        if (result.cleanse !== undefined) {
          result.cleanse = result.cleanse + 1;
        }
        if (result.heal !== undefined) {
          result.heal = result.heal + 5;
        } else {
          result.heal = 5;
        }
        break;
    }
  }

  return result;
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
