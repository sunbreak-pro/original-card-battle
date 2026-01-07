import type { BuffDebuffMap, BuffDebuffState } from "../type/baffType";
import type { Enemy, EnemyAction } from "../../characters/type/enemyType";
import type { Player } from "../../characters/type/playerTypes";
import type { Card } from "../../cards/type/cardType";
import { calculateDamage, applyDamageAllocation } from "../caluculaters/damageCalculation";
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
 */
export function manageEnemyAttackDamage(
    enemyChar: Enemy,
    playerChar: Player,
    action: EnemyAction
): EnemyActionDamageResult {
    const enemyAttackCard = enemyAction(action);
    const damageResult = calculateDamage(enemyChar, playerChar, enemyAttackCard);
    const allocation = applyDamageAllocation(playerChar, damageResult.finalDamage);

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
 */
export function managePlayerCardDamage(
    playerChar: Player,
    enemyChar: Enemy,
    card: Card
): PlayerCardDamageResult {
    const damageResult = calculateDamage(playerChar, enemyChar, card);
    const allocation = applyDamageAllocation(enemyChar, damageResult.finalDamage);

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
