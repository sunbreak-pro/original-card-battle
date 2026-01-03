import type { BuffDebuffMap } from "../../cards/type/baffType";
import type { Card } from "../../cards/type/cardType";

export interface Character {
  name?: string;
  className?: string;
  hp: number;
  maxHp: number;
  ap: number;
  maxAp: number;
  guard: number;
  buffDebuffs: BuffDebuffMap;
  equipmentDefPercent?: number;
}

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

export interface DepthInfo {
  depth: number;
  name: string;
}

const DEPTH_TABLE: Map<number, DepthInfo> = new Map([
  [1, { depth: 1, name: "腐食" }],
  [2, { depth: 2, name: "狂乱" }],
  [3, { depth: 3, name: "混沌" }],
  [4, { depth: 4, name: "虚無" }],
  [5, { depth: 5, name: "深淵" }],
]);

export function getDepthInfo(depth: number): DepthInfo {
  const info = DEPTH_TABLE.get(depth);
  if (!info) {
    return DEPTH_TABLE.get(1)!;
  }
  return info;
}

export function calculateAttackMultiplier(buffDebuffs: BuffDebuffMap): number {
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
export function calculateCriticalRate(buffDebuffs: BuffDebuffMap): number {
  let rate = 0.1;

  if (buffDebuffs.has("criticalUp")) {
    const buff = buffDebuffs.get("criticalUp")!;
    rate += buff.value / 100;
  }

  return Math.min(0.8, rate);
}

function calculateDefenseModifier(buffDebuffs: BuffDebuffMap): {
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

export function calculateReflectDamage(
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

export function calculateDamage(
  attacker: Character,
  defender: Character,
  card: Card,
): DamageResult {
  const baseDmg = card.baseDamage || 0;

  const atkMultiplier = calculateAttackMultiplier(attacker.buffDebuffs);

  let critMod = 1.0;
  let isCritical = false;

  if (attacker.buffDebuffs.has("criticalUp")) {
    const critRate = calculateCriticalRate(attacker.buffDebuffs);
    isCritical = Math.random() < critRate;

    if (isCritical) {
      critMod = 1.5;
      const critBuff = attacker.buffDebuffs.get("criticalUp")!;
      critMod += critBuff.value / 100;
    }
  }
  const finalAtk = Math.floor(baseDmg * atkMultiplier * critMod);
  const { vulnerabilityMod, damageReductionMod } = calculateDefenseModifier(defender.buffDebuffs);
  const incomingDmg = Math.floor(finalAtk * vulnerabilityMod * damageReductionMod);
  const reflectDamage = calculateReflectDamage(defender.buffDebuffs, incomingDmg);
  const lifestealAmount = calculateLifesteal(attacker.buffDebuffs, incomingDmg);
  return {
    finalDamage: incomingDmg,
    isCritical,
    reflectDamage,
    lifestealAmount,
  };
}
export function applyDamageAllocation(
  defender: Character,
  damage: number
): DamageAllocation {
  let remainingDmg = damage;
  let guardDmg = 0;
  let apDmg = 0;
  let hpDmg = 0;
  const hadGuard = defender.guard > 0;

  // Step 2: Damage to Guard (damage as is)
  if (hadGuard) {
    if (defender.guard >= remainingDmg && defender.ap <= 0) {
      guardDmg = remainingDmg;
      hpDmg = Math.floor(remainingDmg * 0.75);
      remainingDmg = 0;
      return { guardDamage: guardDmg, apDamage: apDmg, hpDamage: hpDmg };
    } else if (defender.guard >= remainingDmg) {
      guardDmg = remainingDmg;
      remainingDmg = 0;
      return { guardDamage: guardDmg, apDamage: 0, hpDamage: 0 };
    }
    else {
      guardDmg = defender.guard;
      remainingDmg -= defender.guard;
    }
  }
  if (defender.ap > 0) {
    if (defender.ap >= remainingDmg) {
      apDmg = remainingDmg;
      remainingDmg = 0;
      return { guardDamage: guardDmg, apDamage: apDmg, hpDamage: 0 };
    } else {
      apDmg = defender.ap;
      remainingDmg -= defender.ap;
    }
  }
  if (remainingDmg > 0) {
    hpDmg = remainingDmg;
  }
  return {
    guardDamage: guardDmg,
    apDamage: apDmg,
    hpDamage: hpDmg,
  };
}

