import type { BuffDebuffMap } from "../type/baffType";
import { BUFF_EFFECTS } from "../data/buffData";
import type { BuffDebuffType } from "../type/baffType";
import {
  BASE_CRIT_RATE,
  MAX_CRIT_RATE,
  BASE_HIT_RATE,
  MAX_HIT_RATE,
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
export function criticalRateBuff(buffDebuffs: BuffDebuffMap): number {
    let rate = BASE_CRIT_RATE;
    if (buffDebuffs.has("criticalUp")) {
        const buff = buffDebuffs.get("criticalUp")!;
        rate += buff.value / 100;
    }
    return Math.min(MAX_CRIT_RATE, rate);
}
export function hitRateBuff(buffDebuffs: BuffDebuffMap): number {
    let rate = BASE_HIT_RATE;
    if (buffDebuffs.has("hitRateUp")) {
        const buff = buffDebuffs.get("hitRateUp")!;
        rate += buff.value / 100;
    }
    return Math.min(MAX_HIT_RATE, rate);
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
            case "burn":
                buffDamage += buff.value;
                if (map.has("fireField")) {
                    buffDamage += buff.value * FIRE_FIELD_BONUS_MULTIPLIER;
                }
                break;
            case "poison":
                buffDamage += buff.value;
                break;
        }
    });

    return buffDamage;
};

export const canAct = (map: BuffDebuffMap): boolean => {
    return !map.has("stun");
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

export const immunityBuff = (map: BuffDebuffMap, debuffType: BuffDebuffType): boolean => {
    if (map.has("immunity")) {
        return !BUFF_EFFECTS[debuffType].isDebuff;
    }
    return true;
};