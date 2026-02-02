import type { Card } from '@/types/cardTypes';
import type { SwordEnergyState } from '@/types/characterTypes';
import {
    consumeSwordEnergy,
    consumeAllSwordEnergy,
    addSwordEnergy,
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

    return {
        damageBonus: 0,
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
 * Calculate guard amount from sword energy using card's convertEnergyToGuard property
 */
export function calculateSwordEnergyGuard(
    card: Card,
    currentSwordEnergy: number
): number {
    if (!card.convertEnergyToGuard) return 0;
    return currentSwordEnergy * card.convertEnergyToGuard.multiplier;
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
