import type { Card } from '@/types/cardTypes';

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
