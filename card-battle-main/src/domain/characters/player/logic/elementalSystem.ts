/**
 * Mage Class Ability: Elemental Chain System
 *
 * [Elemental Chain Mechanic]
 * Consecutive use of same-element cards builds chain count.
 * Higher chains provide damage bonuses and special effects.
 *
 * [Chain Effects]
 * - 2 chain: Elemental damage +20%
 * - 3 chain: Element-specific debuff applied
 * - 4+ chain: Elemental explosion (AoE potential)
 *
 * [Charged Elements]
 * Using different elements charges elemental power.
 * Charged elements can be released for special attacks.
 *
 * NOTE: This is a STUB implementation for future development.
 */

import type { Card } from "../../../cards/type/cardType";
import type { ElementalState, ElementType } from "../../type/classAbilityTypes";
import { createInitialElemental } from "../../type/classAbilityTypes";
import type { ClassAbilitySystem, DamageModifier } from "../../classAbility/classAbilitySystem";
import { DEFAULT_DAMAGE_MODIFIER } from "../../classAbility/classAbilitySystem";

// ============================================================
// Constants
// ============================================================

/** Maximum chain count before reset */
export const MAX_CHAIN_COUNT = 10;

/** Damage bonus per chain level */
export const CHAIN_DAMAGE_BONUS: Record<number, number> = {
  2: 0.2,  // +20%
  3: 0.35, // +35%
  4: 0.5,  // +50%
  5: 0.7,  // +70%
};

// ============================================================
// Elemental System Implementation (STUB)
// ============================================================

/**
 * Elemental System for Mage class
 *
 * STUB: Full implementation pending
 */
export const ElementalSystem: ClassAbilitySystem<ElementalState> = {
  /**
   * Initialize elemental state
   */
  initialize(): ElementalState {
    return createInitialElemental();
  },

  /**
   * Handle card play - update chain if elemental card
   */
  onCardPlay(state: ElementalState, card: Card): ElementalState {
    // STUB: Only process cards with element property
    if (!card.element) {
      // Non-elemental card breaks the chain
      return {
        ...state,
        lastElement: null,
        chainCount: 0,
      };
    }

    const isSameElement = state.lastElement === card.element;

    if (isSameElement) {
      // Continue chain
      return {
        ...state,
        chainCount: Math.min(MAX_CHAIN_COUNT, state.chainCount + 1),
      };
    } else {
      // Different element - charge the old element and start new chain
      const newChargedElements = new Map(state.chargedElements);
      if (state.lastElement) {
        const currentCharge = newChargedElements.get(state.lastElement) ?? 0;
        newChargedElements.set(state.lastElement, currentCharge + state.chainCount);
      }

      return {
        ...state,
        lastElement: card.element,
        chainCount: 1,
        chargedElements: newChargedElements,
      };
    }
  },

  /**
   * Handle turn start
   */
  onTurnStart(state: ElementalState): ElementalState {
    // STUB: No turn start effects currently
    return state;
  },

  /**
   * Handle turn end
   */
  onTurnEnd(state: ElementalState): ElementalState {
    // STUB: Chain decays slightly at turn end
    return {
      ...state,
      chainCount: Math.max(0, state.chainCount - 1),
    };
  },

  /**
   * Get damage modifier based on elemental chain
   */
  getDamageModifier(state: ElementalState, card?: Card): DamageModifier {
    // Only apply bonus if playing an elemental card matching current chain
    if (!card?.element || card.element !== state.lastElement) {
      return DEFAULT_DAMAGE_MODIFIER;
    }

    const chainBonus = CHAIN_DAMAGE_BONUS[state.chainCount] ??
      (state.chainCount > 5 ? 0.7 + (state.chainCount - 5) * 0.1 : 0);

    return {
      flatBonus: 0,
      percentMultiplier: 1.0 + chainBonus,
      critBonus: state.chainCount >= 4 ? 0.15 : 0,
      penetration: 0,
    };
  },

  /**
   * Check if elemental action can be performed
   */
  canPerformAction(state: ElementalState, actionId: string): boolean {
    switch (actionId) {
      case "elemental_burst":
        // Can burst if chain count is high enough
        return state.chainCount >= 4;
      case "release_charged":
        // Can release if any charged elements exist
        return state.chargedElements.size > 0;
      default:
        return true;
    }
  },

  /**
   * Get description of current elemental state
   */
  getStateDescription(state: ElementalState): string {
    if (state.chainCount === 0) {
      return "属性連鎖なし";
    }

    const elementNames: Record<ElementType, string> = {
      fire: "火",
      ice: "氷",
      lightning: "雷",
      earth: "地",
      wind: "風",
      dark: "闇",
      light: "光",
    };

    const elementName = state.lastElement ? elementNames[state.lastElement] : "";
    return `${elementName}属性 ${state.chainCount}連鎖`;
  },
};

// ============================================================
// Helper Functions
// ============================================================

/**
 * Get element-specific debuff for chain level 3+
 */
export function getElementalDebuff(element: ElementType): string | null {
  const debuffs: Record<ElementType, string> = {
    fire: "burn",
    ice: "slow",
    lightning: "stun",
    earth: "defDownMinor",
    wind: "atkDownMinor",
    dark: "curse",
    light: "cleanse", // Light cleanses enemy buffs instead
  };
  return debuffs[element] ?? null;
}

/**
 * Calculate total charged elemental power
 */
export function getTotalChargedPower(state: ElementalState): number {
  let total = 0;
  state.chargedElements.forEach((value) => {
    total += value;
  });
  return total;
}
