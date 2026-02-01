/**
 * Summoner Class Ability: Summon System
 *
 * [Summon Mechanic]
 * Summoner can summon creatures to aid in battle.
 * Maximum 3 active summons at a time.
 *
 * [Summon Types]
 * - Offensive: Deal damage each turn
 * - Defensive: Provide guard/protection
 * - Support: Buff player or debuff enemies
 *
 * [Bond Level]
 * Bond level increases with summon usage.
 * Higher bond provides stronger summon effects.
 *
 * NOTE: This is a STUB implementation for future development.
 */

import type { Card } from '@/types/cardTypes';
import type { SummonState, Summon, SummonType } from '@/types/characterTypes';
import { createInitialSummon } from '../classAbility/classAbilityUtils';
import type { ClassAbilitySystem, DamageModifier } from "../classAbility/classAbilitySystem";
import { MAX_BOND_LEVEL, BOND_DAMAGE_BONUS_PER_LEVEL } from "../../../../constants";

// ============================================================
// Summon System Implementation (STUB)
// ============================================================

/**
 * Summon System for Summoner class
 *
 * STUB: Full implementation pending
 */
export const SummonSystem: ClassAbilitySystem<SummonState> = {
  /**
   * Initialize summon state
   */
  initialize(): SummonState {
    return createInitialSummon();
  },

  /**
   * Handle card play - summon or enhance summons
   */
  onCardPlay(state: SummonState, card: Card): SummonState {
    // STUB: Only process cards with summon properties
    if (!card.summonId && !card.summonEnhancement) {
      return state;
    }

    let newState = { ...state };

    // Handle summon creation
    if (card.summonId && state.activeSummons.length < state.summonSlots) {
      const newSummon = createSummonFromId(card.summonId, state.bondLevel);
      if (newSummon) {
        newState = {
          ...newState,
          activeSummons: [...state.activeSummons, newSummon],
        };
      }
    }

    // Handle summon enhancement
    if (card.summonEnhancement && state.activeSummons.length > 0) {
      newState = {
        ...newState,
        activeSummons: state.activeSummons.map((summon) => ({
          ...summon,
          hp: Math.min(summon.maxHp, summon.hp + card.summonEnhancement!),
          duration: summon.duration + 1,
        })),
      };
    }

    return newState;
  },

  /**
   * Handle turn start - summons perform actions
   */
  onTurnStart(state: SummonState): SummonState {
    // STUB: Summons would perform their actions here
    // For now, just decrement duration
    const updatedSummons = state.activeSummons
      .map((summon) => ({
        ...summon,
        duration: summon.duration - 1,
      }))
      .filter((summon) => summon.duration > 0);

    return {
      ...state,
      activeSummons: updatedSummons,
    };
  },

  /**
   * Handle turn end
   */
  onTurnEnd(state: SummonState): SummonState {
    // STUB: No turn end effects currently
    return state;
  },

  /**
   * Get damage modifier based on active summons
   */
  getDamageModifier(state: SummonState, _card?: Card): DamageModifier {
    // Bonus from bond level
    const bondBonus = state.bondLevel * BOND_DAMAGE_BONUS_PER_LEVEL;

    // Bonus from offensive summons
    const offensiveSummons = state.activeSummons.filter((s) => s.type === "offensive");
    const summonBonus = offensiveSummons.length * 0.1; // +10% per offensive summon

    return {
      flatBonus: 0,
      percentMultiplier: 1.0 + bondBonus + summonBonus,
      critBonus: 0,
      penetration: 0,
    };
  },

  /**
   * Check if summon action can be performed
   */
  canPerformAction(state: SummonState, actionId: string): boolean {
    switch (actionId) {
      case "summon":
        // Can summon if slots available
        return state.activeSummons.length < state.summonSlots;
      case "sacrifice":
        // Can sacrifice if summons exist
        return state.activeSummons.length > 0;
      case "enhance":
        // Can enhance if summons exist
        return state.activeSummons.length > 0;
      default:
        return true;
    }
  },

  /**
   * Get description of current summon state
   */
  getStateDescription(state: SummonState): string {
    if (state.activeSummons.length === 0) {
      return `召喚獣なし (絆Lv.${state.bondLevel})`;
    }

    const summonNames = state.activeSummons.map((s) => s.name).join(", ");
    return `召喚中: ${summonNames} (絆Lv.${state.bondLevel})`;
  },
};

// ============================================================
// Helper Functions
// ============================================================

/**
 * Create a summon from its ID
 * STUB: Would load from summon database in full implementation
 */
function createSummonFromId(summonId: string, bondLevel: number): Summon | null {
  // STUB summon data
  const summonData: Record<string, Omit<Summon, "id">> = {
    wolf: {
      name: "シャドウウルフ",
      type: "offensive",
      hp: 20 + bondLevel * 2,
      maxHp: 20 + bondLevel * 2,
      duration: 3,
      abilities: [],
    },
    golem: {
      name: "ストーンゴーレム",
      type: "defensive",
      hp: 40 + bondLevel * 4,
      maxHp: 40 + bondLevel * 4,
      duration: 4,
      abilities: [],
    },
    fairy: {
      name: "ヒーリングフェアリー",
      type: "support",
      hp: 15 + bondLevel,
      maxHp: 15 + bondLevel,
      duration: 3,
      abilities: [],
    },
  };

  const data = summonData[summonId];
  if (!data) return null;

  return {
    id: `${summonId}_${Date.now()}`,
    ...data,
  };
}

/**
 * Calculate total summon power
 */
export function getTotalSummonPower(state: SummonState): number {
  return state.activeSummons.reduce((total, summon) => total + summon.hp, 0);
}

/**
 * Get summons by type
 */
export function getSummonsByType(state: SummonState, type: SummonType): Summon[] {
  return state.activeSummons.filter((summon) => summon.type === type);
}

/**
 * Increase bond level
 */
export function increaseBondLevel(state: SummonState, amount: number = 1): SummonState {
  return {
    ...state,
    bondLevel: Math.min(MAX_BOND_LEVEL, state.bondLevel + amount),
  };
}
