/**
 * Inn Logic
 *
 * Business logic for the Inn facility including:
 * - Rest option purchases
 * - Meal option purchases
 * - Inn buffs state management
 */

import type { InnBuffsState, RestOption, MealOption, InnEffect } from "@/types/campTypes";
import type { BuffDebuffType } from "@/types/battleTypes";
import { REST_OPTIONS, MEAL_OPTIONS, DEFAULT_INN_BUFFS_STATE } from "@/constants/data/camps/InnData";

// ============================================================
// Affordability Checks
// ============================================================

/**
 * Check if player can afford an inn service
 */
export function canAffordInnService(playerGold: number, cost: number): boolean {
  return playerGold >= cost;
}

/**
 * Get a rest option by ID
 */
export function getRestOption(id: string): RestOption | undefined {
  return REST_OPTIONS.find((opt) => opt.id === id);
}

/**
 * Get a meal option by ID
 */
export function getMealOption(id: string): MealOption | undefined {
  return MEAL_OPTIONS.find((opt) => opt.id === id);
}

// ============================================================
// Effect Application
// ============================================================

/**
 * Apply rest effects to inn buffs state
 */
export function applyRestEffects(
  currentState: InnBuffsState,
  restOption: RestOption
): InnBuffsState {
  const newState = { ...currentState };

  for (const effect of restOption.effects) {
    applyEffect(newState, effect);
  }

  return newState;
}

/**
 * Apply meal effects to inn buffs state
 */
export function applyMealEffects(
  currentState: InnBuffsState,
  mealOption: MealOption
): InnBuffsState {
  const newState = { ...currentState };

  for (const effect of mealOption.effects) {
    applyEffect(newState, effect, mealOption.duration);
  }

  return newState;
}

/**
 * Apply a single effect to inn buffs state
 */
function applyEffect(
  state: InnBuffsState,
  effect: InnEffect,
  defaultDuration?: number
): void {
  switch (effect.type) {
    case "bonusHp":
      state.bonusHp += effect.value;
      break;
    case "bonusAp":
      state.bonusAp += effect.value;
      break;
    case "energyBonus":
      state.bonusEnergy += effect.value;
      break;
    case "hpRegenPercent":
      state.hpRegenPercent += effect.value;
      break;
    case "goldBonus":
      state.goldBonusPercent += effect.value;
      break;
    case "buff":
      if (effect.buffType) {
        const duration = effect.duration ?? defaultDuration ?? 3;
        state.startingBuffs.push({
          buffType: effect.buffType,
          duration,
          stacks: effect.value,
        });
      }
      break;
  }
}

// ============================================================
// State Management
// ============================================================

/**
 * Clear inn buffs after they've been consumed (exploration start)
 */
export function clearInnBuffs(): InnBuffsState {
  return {
    ...DEFAULT_INN_BUFFS_STATE,
    startingBuffs: [],
    consumed: false,
  };
}

/**
 * Mark inn buffs as consumed
 */
export function markInnBuffsConsumed(state: InnBuffsState): InnBuffsState {
  return {
    ...state,
    consumed: true,
  };
}

/**
 * Check if there are any active inn buffs
 */
export function hasActiveInnBuffs(state: InnBuffsState): boolean {
  if (state.consumed) return false;

  return (
    state.bonusHp > 0 ||
    state.bonusAp > 0 ||
    state.bonusEnergy > 0 ||
    state.startingBuffs.length > 0 ||
    state.hpRegenPercent > 0 ||
    state.goldBonusPercent > 0
  );
}

/**
 * Create a summary of active inn buffs for display
 */
export function getInnBuffsSummary(state: InnBuffsState): string[] {
  const summary: string[] = [];

  if (state.bonusHp > 0) {
    summary.push(`最大HP +${state.bonusHp}`);
  }
  if (state.bonusAp > 0) {
    summary.push(`AP +${state.bonusAp}`);
  }
  if (state.bonusEnergy > 0) {
    summary.push(`初期エネルギー +${state.bonusEnergy}`);
  }
  if (state.hpRegenPercent > 0) {
    summary.push(`戦闘後HP回復 +${state.hpRegenPercent}%`);
  }
  if (state.goldBonusPercent > 0) {
    summary.push(`獲得ゴールド +${state.goldBonusPercent}%`);
  }
  for (const buff of state.startingBuffs) {
    summary.push(`${getBuffDisplayName(buff.buffType)} (${buff.duration}戦闘)`);
  }

  return summary;
}

/**
 * Get display name for a buff type (Japanese)
 */
function getBuffDisplayName(buffType: BuffDebuffType): string {
  const buffNames: Partial<Record<BuffDebuffType, string>> = {
    atkUpMinor: "攻撃力上昇(小)",
    atkUpMajor: "攻撃力上昇(大)",
    defUpMinor: "防御力上昇(小)",
    defUpMajor: "防御力上昇(大)",
    haste: "加速",
    superFast: "超加速",
    regeneration: "リジェネ",
    shieldRegen: "シールドリジェネ",
    criticalUp: "クリティカル率上昇",
    hitRateUp: "命中率上昇",
    penetrationUp: "貫通上昇",
  };

  return buffNames[buffType] ?? buffType;
}

// ============================================================
// Purchase Results
// ============================================================

export interface InnPurchaseResult {
  success: boolean;
  message: string;
  newGold?: number;
  newInnBuffs?: InnBuffsState;
}

/**
 * Process a rest purchase
 */
export function purchaseRest(
  playerGold: number,
  currentInnBuffs: InnBuffsState,
  restOptionId: string
): InnPurchaseResult {
  const restOption = getRestOption(restOptionId);

  if (!restOption) {
    return {
      success: false,
      message: "休息オプションが見つかりません",
    };
  }

  if (!canAffordInnService(playerGold, restOption.cost)) {
    return {
      success: false,
      message: "ゴールドが足りません",
    };
  }

  const newInnBuffs = applyRestEffects(currentInnBuffs, restOption);

  return {
    success: true,
    message: restOption.cost > 0
      ? `${restOption.nameJa}で休息しました`
      : "休憩しました",
    newGold: playerGold - restOption.cost,
    newInnBuffs,
  };
}

/**
 * Process a meal purchase
 */
export function purchaseMeal(
  playerGold: number,
  currentInnBuffs: InnBuffsState,
  mealOptionId: string
): InnPurchaseResult {
  const mealOption = getMealOption(mealOptionId);

  if (!mealOption) {
    return {
      success: false,
      message: "食事オプションが見つかりません",
    };
  }

  if (!canAffordInnService(playerGold, mealOption.cost)) {
    return {
      success: false,
      message: "ゴールドが足りません",
    };
  }

  const newInnBuffs = applyMealEffects(currentInnBuffs, mealOption);

  return {
    success: true,
    message: `${mealOption.nameJa}を注文しました`,
    newGold: playerGold - mealOption.cost,
    newInnBuffs,
  };
}
