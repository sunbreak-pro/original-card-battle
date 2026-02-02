import type { BuffDebuffMap } from '@/types/battleTypes';
import {
    CURSE_HEALING_MULTIPLIER,
    OVER_CURSE_HEALING_MULTIPLIER,
    FIRE_FIELD_BONUS_MULTIPLIER,
} from "../../../constants";

export function attackBuffDebuff(buffDebuffs: BuffDebuffMap): number {
    let multiplier = 1.0;
    if (buffDebuffs.has("atkUpMinor")) {
        const buff = buffDebuffs.get("atkUpMinor")!;
        multiplier += buff.value / 100;
    }
    if (buffDebuffs.has("atkUpMajor")) {
        const buff = buffDebuffs.get("atkUpMajor")!;
        multiplier += buff.value / 100;
    }
    if (buffDebuffs.has("momentum")) {
        const momentum = buffDebuffs.get("momentum")!;
        multiplier += (momentum.value / 100) * momentum.stacks;
    }
    if (buffDebuffs.has("atkDownMinor")) {
        const buff = buffDebuffs.get("atkDownMinor")!;
        multiplier *= 1 - buff.value / 100;
    }
    if (buffDebuffs.has("atkDownMajor")) {
        const buff = buffDebuffs.get("atkDownMajor")!;
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
    if (buffDebuffs.has("defUpMinor")) {
        const buff = buffDebuffs.get("defUpMinor")!;
        damageReductionMod *= 1 - buff.value / 100;
    }
    if (buffDebuffs.has("defUpMajor")) {
        const buff = buffDebuffs.get("defUpMajor")!;
        damageReductionMod *= 1 - buff.value / 100;
    }
    if (buffDebuffs.has("defDownMinor")) {
        const buff = buffDebuffs.get("defDownMinor")!;
        vulnerabilityMod *= 1 + buff.value / 100;
    }
    if (buffDebuffs.has("defDownMajor")) {
        const buff = buffDebuffs.get("defDownMajor")!;
        vulnerabilityMod *= 1 + buff.value / 100;
    }
    if (buffDebuffs.has("tenacity")) {
        const tenacity = buffDebuffs.get("tenacity")!;
        if (vulnerabilityMod > 1.0) {
            const excessVuln = vulnerabilityMod - 1.0;
            vulnerabilityMod = 1.0 + excessVuln * (1 - tenacity.value / 100);
        }
    }
    return { vulnerabilityMod, damageReductionMod };
}
export function reflectBuff(
    buffDebuffs: BuffDebuffMap,
    damage: number
): number {
    let reflectDamage = 0;

    if (buffDebuffs.has("reflect")) {
        const reflect = buffDebuffs.get("reflect")!;
        reflectDamage = Math.floor(damage * (reflect.value / 100));
    }
    return reflectDamage;
}

export const calculateStartPhaseHealing = (
    map: BuffDebuffMap
): { hp: number; shield: number } => {
    let hp = 0;
    let shield = 0;

    map.forEach((buff) => {
        switch (buff.name) {
            case "regeneration":
                hp += buff.value * buff.stacks;
                break;
            case "shieldRegen":
                shield += buff.value * buff.stacks;
                break;
        }
    });
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
    if (buffDebuffs.has("lifesteal")) {
        const lifesteal = buffDebuffs.get("lifesteal")!;
        healAmount = Math.floor(damage * (lifesteal.value / 100));
    }
    return healAmount;
}

export const calculateEndPhaseDamage = (map: BuffDebuffMap): number => {
    let buffDamage = 0;

    map.forEach((buff) => {
        switch (buff.name) {
            case "burn": {
                const burnDamage = buff.value * buff.stacks;
                buffDamage += burnDamage;
                if (map.has("fireField")) {
                    buffDamage += burnDamage * FIRE_FIELD_BONUS_MULTIPLIER;
                }
                break;
            }
            case "poison":
                buffDamage += buff.value * buff.stacks;
                break;
        }
    });

    return buffDamage;
};

const DISABLING_DEBUFFS = ["stun", "freeze", "stagger"] as const;

export const canAct = (map: BuffDebuffMap): boolean => {
    return !DISABLING_DEBUFFS.some(d => map.has(d));
};

export const energyRegenBuff = (map: BuffDebuffMap): number => {
    let modifier = 0;

    map.forEach((buff) => {
        if (buff.name === "energyRegen") {
            modifier += buff.value;
        }
    });
    return modifier;
};

export const calculateDrawModifier = (map: BuffDebuffMap): number => {
    let modifier = 0;

    map.forEach((buff) => {
        if (buff.name === "drawPower") {
            modifier += buff.value * buff.stacks;
        }
    });

    return modifier;
};

