import type { BuffDebuffMap } from '@/types/battleTypes';
import {
    CURSE_HEALING_MULTIPLIER,
    OVER_CURSE_HEALING_MULTIPLIER,
    FIRE_FIELD_BONUS_MULTIPLIER,
} from "../../../constants";
import {
    ATTACK_BUFF_TYPES,
    ATTACK_DEBUFF_TYPES,
    DAMAGE_REDUCTION_BUFF_TYPES,
    VULNERABILITY_DEBUFF_TYPES,
    START_PHASE_HEALING_BUFFS,
    START_PHASE_SHIELD_BUFFS,
    END_PHASE_DAMAGE_BUFFS,
    REFLECT_BUFFS,
    LIFESTEAL_BUFFS,
    ENERGY_REGEN_BUFFS,
    DRAW_MODIFIER_BUFFS,
    VULNERABILITY_MITIGATION_BUFFS,
} from "@/constants/data/battles/buffData";

export function attackBuffDebuff(buffDebuffs: BuffDebuffMap): number {
    let multiplier = 1.0;

    // Apply attack buffs (additive to base multiplier)
    for (const entry of ATTACK_BUFF_TYPES) {
        const buff = buffDebuffs.get(entry.type);
        if (!buff) continue;
        if (entry.mode === "stackScaled") {
            multiplier += (buff.value / 100) * buff.stacks;
        } else {
            multiplier += buff.value / 100;
        }
    }

    // Apply attack debuffs (multiplicative reduction)
    for (const type of ATTACK_DEBUFF_TYPES) {
        const buff = buffDebuffs.get(type);
        if (!buff) continue;
        multiplier *= 1 - buff.value / 100;
    }

    return multiplier;
}

export function defenseBuffDebuff(buffDebuffs: BuffDebuffMap): {
    vulnerabilityMod: number;
    damageReductionMod: number;
} {
    let vulnerabilityMod = 1.0;
    let damageReductionMod = 1.0;

    // Apply damage reduction buffs (multiplicative)
    for (const type of DAMAGE_REDUCTION_BUFF_TYPES) {
        const buff = buffDebuffs.get(type);
        if (!buff) continue;
        damageReductionMod *= 1 - buff.value / 100;
    }

    // Apply vulnerability debuffs (multiplicative)
    for (const type of VULNERABILITY_DEBUFF_TYPES) {
        const buff = buffDebuffs.get(type);
        if (!buff) continue;
        vulnerabilityMod *= 1 + buff.value / 100;
    }

    // Vulnerability mitigation buffs reduce excess vulnerability
    for (const type of VULNERABILITY_MITIGATION_BUFFS) {
        const buff = buffDebuffs.get(type);
        if (buff && vulnerabilityMod > 1.0) {
            const excessVuln = vulnerabilityMod - 1.0;
            vulnerabilityMod = 1.0 + excessVuln * (1 - buff.value / 100);
        }
    }

    return { vulnerabilityMod, damageReductionMod };
}
export function reflectBuff(
    buffDebuffs: BuffDebuffMap,
    damage: number
): number {
    let reflectDamage = 0;

    for (const type of REFLECT_BUFFS) {
        const buff = buffDebuffs.get(type);
        if (buff) {
            reflectDamage += Math.floor(damage * (buff.value / 100));
        }
    }
    return reflectDamage;
}

export const calculateStartPhaseHealing = (
    map: BuffDebuffMap
): { hp: number; shield: number } => {
    let hp = 0;
    let shield = 0;

    // Calculate HP healing from healing buffs
    for (const type of START_PHASE_HEALING_BUFFS) {
        const buff = map.get(type);
        if (buff) {
            hp += buff.value * buff.stacks;
        }
    }

    // Calculate shield gain from shield buffs
    for (const type of START_PHASE_SHIELD_BUFFS) {
        const buff = map.get(type);
        if (buff) {
            shield += buff.value * buff.stacks;
        }
    }

    // Apply curse healing reduction
    if (map.has("curse")) {
        hp = Math.floor(hp * CURSE_HEALING_MULTIPLIER);
    }
    if (map.has("overCurse")) {
        hp = Math.floor(hp * OVER_CURSE_HEALING_MULTIPLIER);
    }

    return { hp, shield };
};

export function calculateLifesteal(
    buffDebuffs: BuffDebuffMap,
    damage: number
): number {
    let healAmount = 0;
    for (const type of LIFESTEAL_BUFFS) {
        const buff = buffDebuffs.get(type);
        if (buff) {
            healAmount += Math.floor(damage * (buff.value / 100));
        }
    }
    return healAmount;
}

export const calculateEndPhaseDamage = (map: BuffDebuffMap): number => {
    let buffDamage = 0;

    for (const type of END_PHASE_DAMAGE_BUFFS) {
        const buff = map.get(type);
        if (buff) {
            const baseDamage = buff.value * buff.stacks;
            buffDamage += baseDamage;
            // Fire field bonus applies only to burn damage
            if (type === "burn" && map.has("fireField")) {
                buffDamage += baseDamage * FIRE_FIELD_BONUS_MULTIPLIER;
            }
        }
    }

    return buffDamage;
};

const DISABLING_DEBUFFS = ["stun", "freeze", "stagger"] as const;

export const canAct = (map: BuffDebuffMap): boolean => {
    return !DISABLING_DEBUFFS.some(d => map.has(d));
};

export const energyRegenBuff = (map: BuffDebuffMap): number => {
    let modifier = 0;

    for (const type of ENERGY_REGEN_BUFFS) {
        const buff = map.get(type);
        if (buff) {
            modifier += buff.value * buff.stacks;
        }
    }
    return modifier;
};

export const calculateDrawModifier = (map: BuffDebuffMap): number => {
    let modifier = 0;

    for (const type of DRAW_MODIFIER_BUFFS) {
        const buff = map.get(type);
        if (buff) {
            modifier += buff.value * buff.stacks;
        }
    }

    return modifier;
};

