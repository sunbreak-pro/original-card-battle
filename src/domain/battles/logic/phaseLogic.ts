import type { BuffDebuffMap } from '@/types/battleTypes';
import type { EnemyDefinition } from '@/types/characterTypes';
import {
    calculateStartPhaseHealing,
    calculateEndPhaseDamage,
    canAct,
    calculateDrawModifier,
} from "../calculators/buffCalculation";
import {
    decreaseBuffDebuffDuration,
    removeAllDebuffs,
} from "./buffLogic";
import { PLAYER_BASE_DRAW_COUNT, GUARD_INIT_MULTIPLIER } from "../../../constants";

/**
 * Result of player phase start calculations
 */
export interface PlayerPhaseStartResult {
    newBuffs: BuffDebuffMap;
    healAmount: number;
    shieldAmount: number;
    drawCount: number;
    shouldCleanse: boolean;
}

/**
 * Calculate player phase start effects
 */
export function calculatePlayerPhaseStart(
    playerBuffs: BuffDebuffMap,
    baseDrawCount: number = PLAYER_BASE_DRAW_COUNT
): PlayerPhaseStartResult {
    // Process buff/debuff durations
    let newBuffs = decreaseBuffDebuffDuration(playerBuffs);

    // Start-of-phase healing/shield
    const { hp, shield } = calculateStartPhaseHealing(playerBuffs);

    // Check for cleanse
    const shouldCleanse = playerBuffs.has("cleanse");
    if (shouldCleanse) {
        newBuffs = removeAllDebuffs(newBuffs);
    }

    // Calculate draw modifier
    const drawModifier = calculateDrawModifier(playerBuffs);
    const drawCount = baseDrawCount + drawModifier;

    return {
        newBuffs,
        healAmount: hp,
        shieldAmount: shield,
        drawCount,
        shouldCleanse,
    };
}

/**
 * Result of enemy phase start calculations
 */
export interface EnemyPhaseStartResult {
    newBuffs: BuffDebuffMap;
    healAmount: number;
    shieldAmount: number;
    canPerformAction: boolean;
    guardReset: number;
}

/**
 * Calculate enemy phase start effects
 */
export function calculateEnemyPhaseStart(
    enemy: EnemyDefinition,
    enemyBuffs: BuffDebuffMap,
    _enemyMaxHp: number
): EnemyPhaseStartResult {
    // Process buff/debuff durations
    const newBuffs = decreaseBuffDebuffDuration(enemyBuffs);

    // Start-of-phase healing/shield
    const { hp, shield } = calculateStartPhaseHealing(enemyBuffs);

    // Check if enemy can act (not stunned)
    const canPerformAction = canAct(enemyBuffs);

    // Guard reset value - calculate from baseMaxAp for startingGuard enemies
    const guardReset = enemy.startingGuard ? Math.floor(enemy.baseMaxAp * GUARD_INIT_MULTIPLIER) : 0;

    return {
        newBuffs,
        healAmount: hp,
        shieldAmount: shield,
        canPerformAction,
        guardReset,
    };
}

/**
 * Result of phase end calculations
 */
export interface PhaseEndResult {
    dotDamage: number;
    newBuffs: BuffDebuffMap;
}

/**
 * Calculate player phase end effects
 */
export function calculatePlayerPhaseEnd(
    playerBuffs: BuffDebuffMap
): PhaseEndResult {
    const dotDamage = calculateEndPhaseDamage(playerBuffs);

    // Handle momentum buff stacking
    let newBuffs = new Map(playerBuffs);
    if (newBuffs.has("momentum")) {
        const momentum = newBuffs.get("momentum")!;
        newBuffs.set("momentum", { ...momentum, stacks: momentum.stacks + 1 });
    }

    return {
        dotDamage,
        newBuffs,
    };
}

/**
 * Calculate enemy phase end effects
 */
export function calculateEnemyPhaseEnd(
    enemyBuffs: BuffDebuffMap
): PhaseEndResult {
    const dotDamage = calculateEndPhaseDamage(enemyBuffs);

    return {
        dotDamage,
        newBuffs: new Map(enemyBuffs),
    };
}

/**
 * Check if battle should end
 */
export function checkBattleEndCondition(
    playerHp: number,
    enemiesHp: number[]
): { shouldEnd: boolean; result: "ongoing" | "victory" | "defeat" } {
    const allEnemiesDead = enemiesHp.every(hp => hp <= 0);

    if (allEnemiesDead) {
        return { shouldEnd: true, result: "victory" };
    }
    if (playerHp <= 0) {
        return { shouldEnd: true, result: "defeat" };
    }
    return { shouldEnd: false, result: "ongoing" };
}
