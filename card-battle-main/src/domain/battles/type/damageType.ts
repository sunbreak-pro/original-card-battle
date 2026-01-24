export interface DamageResult {
    finalDamage: number;
    isCritical: boolean;
    reflectDamage: number;
    lifestealAmount: number;
}

export interface DamageAllocation {
    guardDamage: number;
    apDamage: number;
    hpDamage: number;
}

/**
 * Process player card damage against enemy
 */
export interface PlayerCardDamageResult {
    totalDamage: number;
    guardDamage: number;
    apDamage: number;
    hpDamage: number;
    isCritical: boolean;
    lifestealAmount: number;
    reflectDamage: number;
}