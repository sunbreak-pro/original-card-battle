/**
 * Character Phase Execution Management
 * Handles the execution of player and enemy phases.
 */

import { useCallback, type RefObject } from "react";
import type { Card } from "../../cards/type/cardType";
import type { BuffDebuffMap } from "../type/baffType";
import type { Enemy, EnemyAction } from "../../characters/type/enemyType";
import type { Player } from "../../characters/type/playerTypes";
import type { EnemyBattleState } from "../type/battleStateType";
import type { DeckState } from "../../cards/decks/deckReducter";

// Execution logic
import {
    calculatePlayerPhaseStart,
    applyHealWithCap,
} from "../execution/playerPhaseExecution";
import {
    calculateEnemyPhaseStart,
    calculateEnemyPhaseEnd,
    calculateEnemyAttackDamage,
    applyEnemyDebuffsToPlayer,
} from "../execution/enemyPhaseExecution";

// Other imports
import { applyHeal } from "../logic/battleLogic";
import { calculateBleedDamage } from "../logic/bleedDamage";
import { executeEnemyActions } from "../../characters/enemy/logic/enemyActionExecution";

// ============================================================================
// Types
// ============================================================================

export interface PhaseStateActions {
    setPlayerPhaseActive: () => void;
    setEnemyPhaseActive: () => void;
    incrementPhaseCount: () => void;
    clearActivePhase: () => void;
}

export interface PlayerPhaseContext {
    // State
    playerBuffs: BuffDebuffMap;
    playerMaxHp: number;
    maxEnergy: number;
    deckStateRef: RefObject<DeckState>;
    drawnCardsRef: RefObject<Card[]>;
    playerRef: RefObject<HTMLDivElement | null>;

    // Setters
    setPlayerGuard: (updater: number | ((prev: number) => number)) => void;
    setPlayerEnergy: (updater: number | ((prev: number) => number)) => void;
    setPlayerBuffs: (updater: BuffDebuffMap | ((prev: BuffDebuffMap) => BuffDebuffMap)) => void;
    setPlayerHp: (updater: number | ((prev: number) => number)) => void;
    setIsShuffling: (value: boolean) => void;
    setIsDrawingAnimation: (value: boolean) => void;

    // Animation
    showMessage: (message: string, duration: number, callback?: () => void) => void;
    showHealEffect: (target: HTMLElement, amount: number) => void;
    showShieldEffect: (target: HTMLElement, amount: number) => void;

    // Phase state actions
    phaseState: PhaseStateActions;

    // Deck dispatch
    dispatch: (action: { type: "SET_PILES"; newDrawPile: Card[]; newDiscardPile: Card[] }) => void;
}

export interface EnemyPhaseContext {
    // State
    currentEnemy: Enemy;
    enemyBuffs: BuffDebuffMap;
    enemyHp: number;
    enemyMaxHp: number;
    enemyEnergy: number;
    playerHp: number;
    playerBuffs: BuffDebuffMap;
    enemies: EnemyBattleState[];
    enemyChar: Enemy;
    playerChar: Player;
    playerRef: RefObject<HTMLDivElement | null>;

    // Setters
    setEnemyGuard: (updater: number | ((prev: number) => number)) => void;
    setEnemyEnergy: (value: number) => void;
    setEnemyBuffs: (updater: BuffDebuffMap | ((prev: BuffDebuffMap) => BuffDebuffMap)) => void;
    setEnemyHp: (updater: number | ((prev: number) => number)) => void;
    setPlayerGuard: (updater: number | ((prev: number) => number)) => void;
    setPlayerAp: (updater: number | ((prev: number) => number)) => void;
    setPlayerHp: (updater: number | ((prev: number) => number)) => void;
    setPlayerBuffs: (updater: BuffDebuffMap | ((prev: BuffDebuffMap) => BuffDebuffMap)) => void;
    setBattleStats: (updater: (prev: { damageDealt: number; damageTaken: number }) => { damageDealt: number; damageTaken: number }) => void;

    // Animation
    showMessage: (message: string, duration: number, callback?: () => void) => void;
    showDamageEffect: (target: HTMLElement, amount: number, isCritical: boolean) => void;
    getTargetEnemyRef: () => HTMLElement | null;

    // Phase state actions
    phaseState: PhaseStateActions;
}

// ============================================================================
// Hook
// ============================================================================

export function useCharacterPhaseExecution() {
    /**
     * Execute player phase
     */
    const executePlayerPhase = useCallback(async (ctx: PlayerPhaseContext) => {
        await new Promise<void>(r => ctx.showMessage("Player Phase", 2500, r));

        // Set phase state
        ctx.phaseState.setPlayerPhaseActive();
        ctx.phaseState.incrementPhaseCount();

        // Calculate phase start effects
        const phaseStartResult = calculatePlayerPhaseStart({
            playerBuffs: ctx.playerBuffs,
            drawPile: ctx.deckStateRef.current.drawPile,
            discardPile: ctx.deckStateRef.current.discardPile,
        });

        // Reset guard and energy
        ctx.setPlayerGuard(0);
        ctx.setPlayerEnergy(ctx.maxEnergy);

        // Process buffs
        ctx.setPlayerBuffs(phaseStartResult.newBuffs);

        // Apply healing
        if (phaseStartResult.healAmount > 0) {
            ctx.setPlayerHp(h => applyHealWithCap(phaseStartResult.healAmount, h, ctx.playerMaxHp));
            if (ctx.playerRef.current) ctx.showHealEffect(ctx.playerRef.current, phaseStartResult.healAmount);
            await new Promise(r => setTimeout(r, 500));
        }

        // Apply shield
        if (phaseStartResult.shieldAmount > 0) {
            ctx.setPlayerGuard(g => g + phaseStartResult.shieldAmount);
            if (ctx.playerRef.current) ctx.showShieldEffect(ctx.playerRef.current, phaseStartResult.shieldAmount);
            await new Promise(r => setTimeout(r, 500));
        }

        // Draw cards
        ctx.drawnCardsRef.current = phaseStartResult.drawnCards;
        ctx.dispatch({
            type: "SET_PILES",
            newDrawPile: phaseStartResult.newDrawPile,
            newDiscardPile: phaseStartResult.newDiscardPile,
        });

        if (phaseStartResult.needsShuffle) {
            ctx.setIsShuffling(true);
        } else if (phaseStartResult.drawnCards.length > 0) {
            ctx.setIsDrawingAnimation(true);
        }
    }, []);

    /**
     * Execute enemy phase
     */
    const executeEnemyPhase = useCallback(async (ctx: EnemyPhaseContext) => {
        await new Promise<void>(r => ctx.showMessage("Enemy Phase", 2500, r));

        // Set phase state
        ctx.phaseState.setEnemyPhaseActive();
        ctx.phaseState.incrementPhaseCount();

        // Calculate phase start effects
        const phaseStartResult = calculateEnemyPhaseStart({
            enemy: ctx.currentEnemy,
            enemyBuffs: ctx.enemyBuffs,
        });

        // Apply phase start effects
        ctx.setEnemyGuard(phaseStartResult.guardReset);
        ctx.setEnemyEnergy(phaseStartResult.energyReset);
        ctx.setEnemyBuffs(phaseStartResult.newBuffs);

        // Apply healing
        if (phaseStartResult.healAmount > 0) {
            ctx.setEnemyHp(h => applyHeal(phaseStartResult.healAmount, h, ctx.enemyMaxHp));
            await new Promise(r => setTimeout(r, 500));
        }

        // Apply shield
        if (phaseStartResult.shieldAmount > 0) {
            ctx.setEnemyGuard(g => g + phaseStartResult.shieldAmount);
            await new Promise(r => setTimeout(r, 500));
        }

        // Check if stunned
        if (!phaseStartResult.canPerformAction) {
            await new Promise(r => setTimeout(r, 1500));
            ctx.phaseState.clearActivePhase();
            return;
        }

        await new Promise(r => setTimeout(r, 800));

        // Execute enemy actions
        const checkCharacterHp = () => ctx.playerHp <= 0 || ctx.enemyHp <= 0;

        const onExecuteAction = async (action: EnemyAction) => {
            // Guard-only action
            if (action.guardGain && action.guardGain > 0 && !action.baseDamage) {
                ctx.setEnemyGuard(g => g + action.guardGain!);
                return;
            }

            // Attack action
            const hitCount = action.hitCount || 1;
            for (let i = 0; i < hitCount; i++) {
                const attackResult = calculateEnemyAttackDamage(ctx.enemyChar, ctx.playerChar, action);

                ctx.setPlayerGuard(g => Math.max(0, g - attackResult.guardDamage));
                ctx.setPlayerAp(a => Math.max(0, a - attackResult.apDamage));
                ctx.setPlayerHp(h => Math.max(0, h - attackResult.hpDamage));

                if (ctx.playerRef.current) {
                    ctx.showDamageEffect(ctx.playerRef.current, attackResult.totalDamage, false);
                }

                if (attackResult.reflectDamage > 0) {
                    ctx.setEnemyHp(h => Math.max(0, h - attackResult.reflectDamage));
                    const reflectTarget = ctx.getTargetEnemyRef();
                    if (reflectTarget) ctx.showDamageEffect(reflectTarget, attackResult.reflectDamage, false);
                }

                ctx.setBattleStats(stats => ({
                    ...stats,
                    damageTaken: stats.damageTaken + attackResult.totalDamage,
                }));

                if (i < hitCount - 1) {
                    await new Promise(r => setTimeout(r, 500));
                }
            }

            // Apply debuffs
            if (action.applyDebuffs && action.applyDebuffs.length > 0) {
                const newBuffs = applyEnemyDebuffsToPlayer(ctx.playerBuffs, action.applyDebuffs);
                ctx.setPlayerBuffs(newBuffs);
            }

            // Bleed damage
            const bleedDamage = calculateBleedDamage(ctx.enemyMaxHp, ctx.enemyBuffs);
            if (bleedDamage > 0) {
                ctx.setEnemyHp(h => Math.max(0, h - bleedDamage));
                const bleedTarget = ctx.getTargetEnemyRef();
                if (bleedTarget) ctx.showDamageEffect(bleedTarget, bleedDamage, false);
                await new Promise(r => setTimeout(r, 300));
            }
        };

        const enemyPhaseCount = ctx.enemies[0]?.turnCount ?? 1;
        await executeEnemyActions(
            ctx.currentEnemy,
            ctx.enemyHp,
            ctx.enemyMaxHp,
            enemyPhaseCount,
            ctx.enemyEnergy,
            onExecuteAction,
            checkCharacterHp
        );

        // Phase end effects
        await new Promise(r => setTimeout(r, 800));
        const phaseEndResult = calculateEnemyPhaseEnd({ enemyBuffs: ctx.enemyBuffs });
        if (phaseEndResult.dotDamage > 0) {
            ctx.setEnemyHp(h => Math.max(0, h - phaseEndResult.dotDamage));
            const dotTarget = ctx.getTargetEnemyRef();
            if (dotTarget) ctx.showDamageEffect(dotTarget, phaseEndResult.dotDamage, false);
            await new Promise(r => setTimeout(r, 500));
        }

        ctx.phaseState.clearActivePhase();
    }, []);

    return {
        executePlayerPhase,
        executeEnemyPhase,
    };
}
