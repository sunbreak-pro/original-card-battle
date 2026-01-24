import type { Card } from "../type/cardType";
import type { SwordEnergyState } from "../../characters/player/logic/swordEnergySystem";
import {
    consumeSwordEnergy,
    consumeAllSwordEnergy,
    addSwordEnergy,
    calculateSwordEnergyConsumeDamage,
} from "../../characters/player/logic/swordEnergySystem";

/**
 * Result of processing sword energy for a card
 */
export interface SwordEnergyProcessResult {
    damageBonus: number;
    consumedAmount: number;
    newState: SwordEnergyState;
}

/**
 * Process sword energy consumption for a card
 */
export function processSwordEnergyConsumption(
    card: Card,
    currentSwordEnergy: SwordEnergyState
): SwordEnergyProcessResult {
    if (card.swordEnergyConsume === undefined) {
        return {
            damageBonus: 0,
            consumedAmount: 0,
            newState: currentSwordEnergy,
        };
    }

    let consumedAmount = 0;
    let newState: SwordEnergyState;

    if (card.swordEnergyConsume === 0) {
        // Consume all sword energy
        const result = consumeAllSwordEnergy(currentSwordEnergy);
        consumedAmount = result.consumed;
        newState = result.newState;
    } else {
        // Consume specified amount
        const result = consumeSwordEnergy(currentSwordEnergy, card.swordEnergyConsume);
        consumedAmount = result.consumed;
        newState = result.newState;
    }

    // Calculate damage bonus
    let damageBonus = 0;
    if (card.swordEnergyMultiplier) {
        damageBonus = calculateSwordEnergyConsumeDamage(
            0,
            consumedAmount,
            card.swordEnergyMultiplier
        );
    }

    return {
        damageBonus,
        consumedAmount,
        newState,
    };
}

/**
 * Process sword energy gain for a card
 */
export function processSwordEnergyGain(
    card: Card,
    currentSwordEnergy: SwordEnergyState
): SwordEnergyState {
    if (!card.swordEnergyGain) {
        return currentSwordEnergy;
    }
    return addSwordEnergy(currentSwordEnergy, card.swordEnergyGain);
}

/**
 * Calculate guard amount from sword energy for specific cards
 */
export function calculateSwordEnergyGuard(
    cardTypeId: string,
    currentSwordEnergy: number
): number {
    switch (cardTypeId) {
        case "sw_037":
            return currentSwordEnergy * 8;
        case "sw_039":
        case "sw_040":
            return currentSwordEnergy * 2;
        default:
            return 0;
    }
}

/**
 * Create a card with sword energy damage bonus applied
 */
export function applyDamageBonusToCard(
    card: Card,
    damageBonus: number
): Card {
    if (damageBonus <= 0) {
        return card;
    }
    return {
        ...card,
        baseDamage: (card.baseDamage || 0) + damageBonus,
    };
}
