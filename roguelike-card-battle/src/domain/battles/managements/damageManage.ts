import type { BuffDebuffMap, BuffDebuffState } from "../type/baffType";
import type { EnemyAction } from "../../characters/type/enemyType";
import type { Card } from "../../cards/type/cardType";
import type { BattleStats } from "../../characters/type/baseTypes";
import { calculateDamage, applyDamageAllocation } from "../calculators/damageCalculation";
import { addOrUpdateBuffDebuff } from "../logic/buffLogic";
import { calculateBleedDamage } from "../logic/bleedDamage";
import { enemyAction } from "../../characters/enemy/logic/enemyAI";
import type { PlayerCardDamageResult } from "../type/damageType";

/**
 * Result of processing enemy action damage
 */
export interface EnemyActionDamageResult {
    totalDamage: number;
    guardDamage: number;
    apDamage: number;
    hpDamage: number;
    reflectDamage: number;
}

/**
 * Calculate damage from a single enemy attack
 *
 * @param attacker - BattleStats of the attacking enemy
 * @param defender - BattleStats of the defending player
 * @param action - Enemy action being executed
 */
export function manageEnemyAttackDamage(
    attacker: BattleStats,
    defender: BattleStats,
    action: EnemyAction
): EnemyActionDamageResult {
    const enemyAttackCard = enemyAction(action);
    const damageResult = calculateDamage(attacker, defender, enemyAttackCard);
    const allocation = applyDamageAllocation(defender, damageResult.finalDamage);

    return {
        totalDamage: damageResult.finalDamage,
        guardDamage: allocation.guardDamage,
        apDamage: allocation.apDamage,
        hpDamage: allocation.hpDamage,
        reflectDamage: damageResult.reflectDamage,
    };
}

/**
 * Process debuffs applied by enemy action
 */
export function processEnemyActionDebuffs(
    action: EnemyAction,
    currentBuffs: BuffDebuffMap
): BuffDebuffMap {
    if (!action.applyDebuffs || action.applyDebuffs.length === 0) {
        return currentBuffs;
    }

    let newBuffs = currentBuffs;
    action.applyDebuffs.forEach((debuff: BuffDebuffState) => {
        newBuffs = addOrUpdateBuffDebuff(
            newBuffs,
            debuff.name,
            debuff.stacks,
            debuff.duration,
            debuff.value,
            debuff.isPermanent,
            debuff.source
        );
    });
    return newBuffs;
}

/**
 * Calculate bleed damage at end of action
 */
export function manageActionBleedDamage(
    maxHp: number,
    buffs: BuffDebuffMap
): number {
    return calculateBleedDamage(maxHp, buffs);
}

/**
 * Calculate damage from player card to enemy
 *
 * @param attacker - BattleStats of the attacking player
 * @param defender - BattleStats of the defending enemy
 * @param card - Card being played
 */
export function managePlayerCardDamage(
    attacker: BattleStats,
    defender: BattleStats,
    card: Card
): PlayerCardDamageResult {
    const damageResult = calculateDamage(attacker, defender, card);
    const allocation = applyDamageAllocation(defender, damageResult.finalDamage);

    return {
        totalDamage: damageResult.finalDamage,
        guardDamage: allocation.guardDamage,
        apDamage: allocation.apDamage,
        hpDamage: allocation.hpDamage,
        isCritical: damageResult.isCritical,
        lifestealAmount: damageResult.lifestealAmount,
        reflectDamage: damageResult.reflectDamage,
    };
}
