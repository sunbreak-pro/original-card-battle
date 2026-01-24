/**
 * Player Phase Execution
 * Contains pure functions for calculating player phase effects.
 * State updates are handled via callbacks in the main hook.
 */

import type { BuffDebuffMap } from "../type/baffType";
import type { Card } from "../../cards/type/cardType";
import {
    calculateStartPhaseHealing,
    calculateEndPhaseDamage,
    calculateDrawModifier,
} from "../calculators/buffCalculation";
import {
    decreaseBuffDebuffDuration,
    removeAllDebuffs,
} from "../logic/buffLogic";
import { drawCards } from "../../cards/decks/deck";

// ============================================================================
// Types
// ============================================================================

export interface PlayerPhaseStartInput {
    playerBuffs: BuffDebuffMap;
    drawPile: Card[];
    discardPile: Card[];
    baseDrawCount?: number;
}

export interface PlayerPhaseStartResult {
    // Buff processing
    newBuffs: BuffDebuffMap;
    shouldCleanse: boolean;

    // Healing/Shield effects
    healAmount: number;
    shieldAmount: number;

    // Card drawing
    drawCount: number;
    drawnCards: Card[];
    newDrawPile: Card[];
    newDiscardPile: Card[];
    needsShuffle: boolean;
}

export interface PlayerPhaseEndInput {
    playerBuffs: BuffDebuffMap;
}

export interface PlayerPhaseEndResult {
    dotDamage: number;
    newBuffs: BuffDebuffMap;
}

// ============================================================================
// Functions
// ============================================================================

/**
 * Calculate all effects that occur at the start of player phase
 */
export function calculatePlayerPhaseStart(
    input: PlayerPhaseStartInput
): PlayerPhaseStartResult {
    const { playerBuffs, drawPile, discardPile, baseDrawCount = 5 } = input;

    // Process buff/debuff durations
    let newBuffs = decreaseBuffDebuffDuration(playerBuffs);

    // Start-of-phase healing/shield
    const { hp: healAmount, shield: shieldAmount } = calculateStartPhaseHealing(playerBuffs);

    // Check for cleanse
    const shouldCleanse = playerBuffs.has("cleanse");
    if (shouldCleanse) {
        newBuffs = removeAllDebuffs(newBuffs);
    }

    // Calculate draw count
    const drawModifier = calculateDrawModifier(playerBuffs);
    const drawCount = baseDrawCount + drawModifier;

    // Draw cards
    const { drawnCards, newDrawPile, newDiscardPile } = drawCards(
        drawCount,
        drawPile,
        discardPile
    );

    // Check if shuffle occurred
    const needsShuffle = newDiscardPile.length === 0 && discardPile.length > 0;

    return {
        newBuffs,
        shouldCleanse,
        healAmount,
        shieldAmount,
        drawCount,
        drawnCards,
        newDrawPile,
        newDiscardPile,
        needsShuffle,
    };
}

/**
 * Calculate all effects that occur at the end of player phase
 */
export function calculatePlayerPhaseEnd(
    input: PlayerPhaseEndInput
): PlayerPhaseEndResult {
    const { playerBuffs } = input;

    // Calculate DoT damage
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
 * Check if player can act (not stunned)
 */
export function canPlayerAct(playerBuffs: BuffDebuffMap): boolean {
    return !playerBuffs.has("stun");
}

/**
 * Apply heal with max HP cap
 */
export function applyHealWithCap(
    healAmount: number,
    currentHp: number,
    maxHp: number
): number {
    return Math.min(maxHp, currentHp + healAmount);
}
