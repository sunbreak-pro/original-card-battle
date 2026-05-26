/**
 * Enemy Phase Execution
 * Contains pure functions for calculating enemy phase effects.
 * State updates are handled via callbacks in the main hook.
 */

import type { BuffDebuffMap, BuffDebuffState } from '@/types/battleTypes';
import type { EnemyDefinition, EnemyAction } from '@/types/characterTypes';
import type { BattleStats } from '@/types/characterTypes';
import {
    calculateStartPhaseHealing,
    calculateEndPhaseDamage,
    canAct,
} from "../calculators/buffCalculation";
import {
    decreaseBuffDebuffDurationForPhase,
    addOrUpdateBuffDebuff,
} from "../logic/buffLogic";
import { calculateDamage, applyDamageAllocation } from "../calculators/damageCalculation";
import { calculateBleedDamage } from "../logic/bleedDamage";
import { enemyAction } from "../../characters/enemy/logic/enemyAI";
import { GUARD_INIT_MULTIPLIER } from "../../../constants";

// ============================================================================
// Shared Predicates
// ============================================================================

/**
 * Check if an enemy action is guard-only (no attack)
 */
export function isGuardOnlyAction(action: EnemyAction): boolean {
    return !!(action.guardGain && action.guardGain > 0 && !action.baseDamage);
}

// ============================================================================
// Types
// ============================================================================

export interface EnemyPhaseStartInput {
    enemy: EnemyDefinition;
    enemyBuffs: BuffDebuffMap;
}

export interface EnemyPhaseStartResult {
    // Buff processing
    newBuffs: BuffDebuffMap;

    // Healing/Shield effects
    healAmount: number;
    shieldAmount: number;

    // Guard reset
    guardReset: number;

    // Energy recovery
    energyReset: number;

    // Can act check
    canPerformAction: boolean;
}

export interface EnemyPhaseEndInput {
    enemyBuffs: BuffDebuffMap;
}

export interface EnemyPhaseEndResult {
    dotDamage: number;
}

export interface EnemyAttackResult {
    totalDamage: number;
    guardDamage: number;
    apDamage: number;
    hpDamage: number;
    isCritical: boolean;
    reflectDamage: number;
}

export interface EnemyActionResult {
    attackResult: EnemyAttackResult | null;
    guardGain: number;
    debuffsToApply: BuffDebuffState[];
    bleedDamage: number;
}

// ============================================================================
// Functions
// ============================================================================

/**
 * Calculate all effects that occur at the start of enemy phase
 */
export function calculateEnemyPhaseStart(
    input: EnemyPhaseStartInput
): EnemyPhaseStartResult {
    const { enemy, enemyBuffs } = input;

    // Process buff/debuff durations first - only decrease enemy-applied buffs
    // This must happen before other calculations so healing and canAct use post-decrease values (V-PHASE-01, V-PHASE-02)
    const newBuffs = decreaseBuffDebuffDurationForPhase(enemyBuffs, 'enemy');

    // Start-of-phase healing/shield - use newBuffs (post-decrease) for consistency with player phase
    const { hp: healAmount, shield: shieldAmount } = calculateStartPhaseHealing(newBuffs);

    // Guard reset (restore to initial guard value if startingGuard is true)
    const guardReset = enemy.startingGuard ? Math.floor(enemy.baseMaxAp * GUARD_INIT_MULTIPLIER) : 0;

    // Energy recovery to MAX
    const energyReset = enemy.actEnergy;

    // Check if enemy can act (not stunned) - use newBuffs so expired stun allows action
    const canPerformAction = canAct(newBuffs);

    return {
        newBuffs,
        healAmount,
        shieldAmount,
        guardReset,
        energyReset,
        canPerformAction,
    };
}

/**
 * Calculate all effects that occur at the end of enemy phase
 */
export function calculateEnemyPhaseEnd(
    input: EnemyPhaseEndInput
): EnemyPhaseEndResult {
    const { enemyBuffs } = input;

    // Calculate DoT damage
    const dotDamage = calculateEndPhaseDamage(enemyBuffs);

    return {
        dotDamage,
    };
}

/**
 * Calculate damage from a single enemy attack
 *
 * @param attacker - BattleStats of the attacking enemy
 * @param defender - BattleStats of the defending player
 * @param action - Enemy action to execute
 */
export function calculateEnemyAttackDamage(
    attacker: BattleStats,
    defender: BattleStats,
    action: EnemyAction
): EnemyAttackResult {
    if (isGuardOnlyAction(action)) {
        return {
            totalDamage: 0,
            guardDamage: 0,
            apDamage: 0,
            hpDamage: 0,
            isCritical: false,
            reflectDamage: 0,
        };
    }

    // Convert enemy action to Card format
    const enemyAttackCard = enemyAction(action);

    // Calculate damage
    const damageResult = calculateDamage(attacker, defender, enemyAttackCard);
    const allocation = applyDamageAllocation(defender, damageResult.finalDamage);

    return {
        totalDamage: damageResult.finalDamage,
        guardDamage: allocation.guardDamage,
        apDamage: allocation.apDamage,
        hpDamage: allocation.hpDamage,
        isCritical: damageResult.isCritical,
        reflectDamage: damageResult.reflectDamage,
    };
}

/**
 * Process a single enemy action and return all effects
 *
 * @param attacker - BattleStats of the attacking enemy
 * @param defender - BattleStats of the defending player
 * @param action - Enemy action to execute
 * @param enemyMaxHp - Maximum HP of the enemy (for bleed calculation)
 * @param enemyBuffs - Current buffs/debuffs on the enemy
 */
export function processEnemyAction(
    attacker: BattleStats,
    defender: BattleStats,
    action: EnemyAction,
    enemyMaxHp: number,
    enemyBuffs: BuffDebuffMap
): EnemyActionResult {
    if (isGuardOnlyAction(action)) {
        return {
            attackResult: null,
            guardGain: action.guardGain ?? 0,
            debuffsToApply: [],
            bleedDamage: 0,
        };
    }

    // Calculate attack damage
    const attackResult = calculateEnemyAttackDamage(attacker, defender, action);

    // Collect debuffs to apply
    const debuffsToApply = action.applyDebuffs || [];

    // Calculate bleed damage
    const bleedDamage = calculateBleedDamage(enemyMaxHp, enemyBuffs);

    return {
        attackResult,
        guardGain: action.guardGain || 0,
        debuffsToApply,
        bleedDamage,
    };
}

/**
 * Apply debuffs from enemy action to player
 */
export function applyEnemyDebuffsToPlayer(
    currentBuffs: BuffDebuffMap,
    debuffs: BuffDebuffState[]
): BuffDebuffMap {
    if (debuffs.length === 0) {
        return currentBuffs;
    }

    let newBuffs = currentBuffs;
    debuffs.forEach((debuff) => {
        // Fixed parameter order: name, duration, value, stacks (was incorrectly: name, stacks, duration, value)
        // Added 'enemy' as appliedBy since enemy is applying these debuffs
        newBuffs = addOrUpdateBuffDebuff(
            newBuffs,
            debuff.name,
            debuff.duration,
            debuff.value,
            debuff.stacks,
            debuff.isPermanent,
            debuff.source,
            'enemy'
        );
    });

    return newBuffs;
}

/**
 * Check if enemy can act (not stunned)
 */
export function canEnemyAct(enemyBuffs: BuffDebuffMap): boolean {
    return canAct(enemyBuffs);
}
